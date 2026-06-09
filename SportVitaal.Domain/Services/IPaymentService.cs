using System.Threading;

namespace SportVitaal.Domain.Services
{
    public interface IPaymentService
    {
        /// <summary>
        /// Create a payment intent for a membership purchase. Returns a client secret that the client can use to complete the payment flow (Stripe).
        /// </summary>
        Task<string> CreateMembershipPaymentIntentAsync(Guid memberId, decimal amount, string currency, IDictionary<string,string>? metadata = null, CancellationToken ct = default);

        /// <summary>
        /// Handle a raw webhook payload (for example from Stripe). Implementations should validate and raise domain events as required.
        /// </summary>
        Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken ct = default);
    }
}


