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

            var start = dto.StartDate ?? DateTime.UtcNow.Date;
            var amount = prices[dto.Type];

            var metadata = new Dictionary<string,string>
            {
                { "membershipType", dto.Type.ToString() },
                { "startDate", start.ToString("o") }
            };

            var clientSecret = await _paymentService.CreateMembershipPaymentIntentAsync(dto.UserId, amount, "EUR", metadata);

            return Ok(new { clientSecret, amount, currency = "EUR", startDate = start });
        }
    }

    public class PurchaseDto
    {
        public Guid UserId { get; set; }
        public MembershipType Type { get; set; }
        public DateTime? StartDate { get; set; }
    }
}


