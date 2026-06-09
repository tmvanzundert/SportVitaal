using Microsoft.Extensions.Logging;
using SportVitaal.Domain.Services;
using SportVitaal.Domain.ValueObjects;

namespace SportVitaal.Infrastructure.Payments
{
    /// <summary>
    /// Simple in-memory simulated payment service for development/testing.
    /// Creates a fake client secret and can simulate a webhook call to mark payments as succeeded.
    /// This avoids Stripe dependency while we finish the WebApi.
    /// </summary>
    public class SimulatedPaymentService : IPaymentService
    {
        private readonly ILogger<SimulatedPaymentService> _logger;
        private readonly IServiceProvider _sp;

        // store simple in-memory payment intents: id -> (memberId, amount, currency, metadata)
        private readonly Dictionary<string, (Guid memberId, decimal amount, string currency, IDictionary<string,string> metadata)> _intents = new();

        public SimulatedPaymentService(ILogger<SimulatedPaymentService> logger, IServiceProvider sp)
        {
            _logger = logger;
            _sp = sp;
        }

        public Task<string> CreateMembershipPaymentIntentAsync(Guid memberId, decimal amount, string currency, IDictionary<string,string>? metadata = null, CancellationToken ct = default)
        {
            var id = Guid.NewGuid().ToString("D");
            var clientSecret = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(id + ":simulated_secret"));
            _intents[id] = (memberId, amount, currency, metadata ?? new Dictionary<string,string>());
            _logger.LogInformation("Simulated payment intent created: {Id} for member {MemberId} amount {Amount} {Currency}", id, memberId, amount, currency);
            return Task.FromResult(clientSecret);
        }

        public Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken ct = default)
        {
            // Expect a small JSON payload to simulate Stripe event, e.g.: { "event":"payment_succeeded", "paymentId":"<id>", "metadata": {"membershipType":"UnlimitedMonthly","startDate":"2026-06-09T00:00:00Z"} }
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(payload);
                if (!doc.RootElement.TryGetProperty("event", out var ev)) return Task.CompletedTask;
                if (ev.GetString() != "payment_succeeded") return Task.CompletedTask;
                if (!doc.RootElement.TryGetProperty("paymentId", out var pidEl)) return Task.CompletedTask;
                var pid = pidEl.GetString();
                if (string.IsNullOrWhiteSpace(pid)) return Task.CompletedTask;

                if (!_intents.TryGetValue(pid, out var info))
                {
                    _logger.LogWarning("Simulated payment webhook for unknown payment id {Id}", pid);
                    return Task.CompletedTask;
                }

                // Optional metadata from payload overrides stored metadata
                IDictionary<string,string> meta = new Dictionary<string,string>(info.metadata);
                if (doc.RootElement.TryGetProperty("metadata", out var metaEl) && metaEl.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var p in metaEl.EnumerateObject()) meta[p.Name] = p.Value.GetString() ?? string.Empty;
                }

                // If membershipType present, try to call MembershipService to activate membership
                if (meta.TryGetValue("membershipType", out var membershipType) && !string.IsNullOrWhiteSpace(membershipType))
                {
                    var membershipService = _sp.GetService(typeof(SportVitaal.Application.Services.MembershipService)) as SportVitaal.Application.Services.MembershipService;
                    if (membershipService != null)
                    {
                        if (Enum.TryParse<SportVitaal.Domain.Enums.MembershipType>(membershipType, true, out var mt))
                        {
                            var start = DateTime.UtcNow;
                            if (meta.TryGetValue("startDate", out var sd) && DateTime.TryParse(sd, out var parsed)) start = parsed;
                            var money = new Money(info.amount, info.currency);
                            _ = membershipService.PurchaseMembershipAsync(info.memberId, mt, start, money);
                            _logger.LogInformation("Simulated payment succeeded and membership purchase invoked for member {MemberId}", info.memberId);
                        }
                    }
                    else
                    {
                        _logger.LogInformation("MembershipService not available in DI; simulated payment succeeded for member {MemberId}", info.memberId);
                    }
                }
                else
                {
                    _logger.LogInformation("Simulated payment succeeded for member {MemberId} (no membership metadata)", info.memberId);
                }

                // remove intent
                _intents.Remove(pid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing simulated payment webhook payload");
            }

            return Task.CompletedTask;
        }
    }
}

