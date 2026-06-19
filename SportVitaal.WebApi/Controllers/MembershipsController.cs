using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportVitaal.Domain.DomainExceptions;
using SportVitaal.Domain.Repositories;
using SportVitaal.Domain.Services;
using SportVitaal.Domain.Enums;

namespace SportVitaal.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MembershipsController : ControllerBase
    {
        // Monthly/yearly prices in EUR.
        private static readonly Dictionary<MembershipType, decimal> Prices = new()
        {
            { MembershipType.TwiceWeeklyMonthly, 29m },
            { MembershipType.TwiceWeeklyYearly, 299m },
            { MembershipType.UnlimitedMonthly, 55m },
            { MembershipType.UnlimitedYearly, 549m }
        };

        private readonly IPaymentService _paymentService;
        private readonly IUserRepository _userRepository;
        private readonly IMembershipService _membershipService;

        public MembershipsController(IPaymentService paymentService, IUserRepository userRepository, IMembershipService membershipService)
        {
            _paymentService = paymentService;
            _userRepository = userRepository;
            _membershipService = membershipService;
        }

        [HttpPost("purchase")]
        [Authorize]
        public async Task<IActionResult> Purchase([FromBody] PurchaseDto dto)
        {
            if (!Prices.ContainsKey(dto.Type)) return BadRequest("Unknown membership type");
            if (!TryGetUserId(out var userId)) return Unauthorized();

            var today = DateTime.UtcNow.Date;
            var start = dto.StartDate?.Date ?? today;
            if (start < today) return BadRequest("Start date cannot be in the past.");

            var amount = Prices[dto.Type];
            var metadata = new Dictionary<string, string>
            {
                { "membershipType", dto.Type.ToString() },
                // A membership start is a calendar date; store it timezone-agnostic.
                { "startDate", start.ToString("yyyy-MM-dd") }
            };

            var clientSecret = await _paymentService.CreateMembershipPaymentIntentAsync(userId, amount, "EUR", metadata);
            return Ok(new { clientSecret, amount, currency = "EUR", startDate = start });
        }

        // Renew (extend) the current subscription by one more period. Members can buy ahead before an
        // expiring (yearly) subscription lapses.
        [HttpPost("renew")]
        [Authorize]
        public async Task<IActionResult> Renew()
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var user = await _userRepository.GetByIdAsync(userId);
            if (user?.Membership is not { } m) return BadRequest("Je hebt geen abonnement om te verlengen.");

            var amount = Prices[m.Type];
            var metadata = new Dictionary<string, string> { { "action", "renew" } };

            var clientSecret = await _paymentService.CreateMembershipPaymentIntentAsync(userId, amount, "EUR", metadata);
            return Ok(new { clientSecret, amount, currency = "EUR" });
        }

        // Upgrade the current "2x per week" subscription to "Onbeperkt" of the same billing period.
        // Monthly pays the new (higher) rate; yearly pays the pro-rata difference for the time left.
        [HttpPost("upgrade")]
        [Authorize]
        public async Task<IActionResult> Upgrade()
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            var user = await _userRepository.GetByIdAsync(userId);
            if (user?.Membership is not { } m) return BadRequest("Je hebt geen abonnement om uit te breiden.");
            if (!m.IsActive) return BadRequest("Je abonnement is niet actief.");

            var newType = m.Type switch
            {
                MembershipType.TwiceWeeklyMonthly => MembershipType.UnlimitedMonthly,
                MembershipType.TwiceWeeklyYearly => MembershipType.UnlimitedYearly,
                _ => (MembershipType?)null
            };
            if (newType is null) return BadRequest("Dit abonnement kan niet worden uitgebreid.");

            // Pro-rata difference over the remaining part of the current period.
            var fullDiff = Prices[newType.Value] - Prices[m.Type];
            decimal amount = fullDiff;
            if (m.EndDate is { } end)
            {
                var total = (end - m.StartDate).TotalDays;
                var remaining = (end - DateTime.UtcNow).TotalDays;
                var fraction = total > 0 ? Math.Clamp(remaining / total, 0, 1) : 1;
                amount = Math.Round(fullDiff * (decimal)fraction, 2);
            }

            var metadata = new Dictionary<string, string>
            {
                { "action", "upgrade" },
                { "newType", newType.Value.ToString() }
            };

            var clientSecret = await _paymentService.CreateMembershipPaymentIntentAsync(userId, amount, "EUR", metadata);
            return Ok(new { clientSecret, amount, currency = "EUR" });
        }

        // Cancel the current subscription. Allowed for monthly only (yearly cannot be stopped);
        // takes effect at the end of the current paid period.
        [HttpPost("cancel")]
        [Authorize]
        public async Task<IActionResult> Cancel()
        {
            if (!TryGetUserId(out var userId)) return Unauthorized();
            try
            {
                await _membershipService.CancelMembershipAsync(userId, userId);
                return Ok();
            }
            catch (DomainException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private bool TryGetUserId(out Guid userId)
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idClaim, out userId);
        }
    }

    public class PurchaseDto
    {
        public MembershipType Type { get; set; }
        public DateTime? StartDate { get; set; }
    }
}
