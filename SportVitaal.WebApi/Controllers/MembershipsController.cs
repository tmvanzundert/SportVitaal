using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SportVitaal.Domain.Services;
using SportVitaal.Domain.Enums;

namespace SportVitaal.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MembershipsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public MembershipsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost("purchase")]
        [Authorize]
        public async Task<IActionResult> Purchase([FromBody] PurchaseDto dto)
        {
            // Map membership type to prices (EUR)
            var prices = new Dictionary<MembershipType, decimal>
            {
                { MembershipType.TwiceWeeklyMonthly, 29m },
                { MembershipType.TwiceWeeklyYearly, 299m },
                { MembershipType.UnlimitedMonthly, 55m },
                { MembershipType.UnlimitedYearly, 549m }
            };

            if (!prices.ContainsKey(dto.Type)) return BadRequest("Unknown membership type");

            // The buyer is always the authenticated user, never a value from the request body.
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(idClaim) || !Guid.TryParse(idClaim, out var userId))
                return Unauthorized();

            var today = DateTime.UtcNow.Date;
            var start = dto.StartDate?.Date ?? today;
            if (start < today) return BadRequest("Start date cannot be in the past.");

            var amount = prices[dto.Type];

            var metadata = new Dictionary<string,string>
            {
                { "membershipType", dto.Type.ToString() },
                // A membership start is a calendar date; store it timezone-agnostic to avoid
                // UTC/local conversion shifting it by the server's offset.
                { "startDate", start.ToString("yyyy-MM-dd") }
            };

            var clientSecret = await _paymentService.CreateMembershipPaymentIntentAsync(userId, amount, "EUR", metadata);

            return Ok(new { clientSecret, amount, currency = "EUR", startDate = start });
        }
    }

    public class PurchaseDto
    {
        public MembershipType Type { get; set; }
        public DateTime? StartDate { get; set; }
    }
}


