using Microsoft.AspNetCore.Mvc;
using SportVitaal.Domain.Services;

namespace SportVitaal.WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync();
            // Accept a generic signature header so simulated and future providers can be used. Clients may send X-Signature.
            var sig = Request.Headers["X-Signature"].FirstOrDefault() ?? Request.Headers["Stripe-Signature"].FirstOrDefault() ?? string.Empty;

            await _paymentService.HandleWebhookAsync(payload, sig);
            return Ok();
        }
    }
}

