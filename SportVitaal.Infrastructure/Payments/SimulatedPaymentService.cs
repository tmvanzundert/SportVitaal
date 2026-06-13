using Microsoft.Extensions.DependencyInjection;
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

        // Stores in-memory payment intents: id -> (memberId, amount, currency, metadata).
        // This service is a singleton so intents survive between the purchase request and the
        // later webhook request; ConcurrentDictionary guards against concurrent access.
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, (Guid memberId, decimal amount, string currency, IDictionary<string,string> metadata)> _intents = new();

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

        public async Task HandleWebhookAsync(string payload, string signatureHeader, CancellationToken ct = default)
        {
            // Expect a small JSON payload to simulate Stripe event, e.g.: { "event":"payment_succeeded", "paymentId":"<id>", "metadata": {"membershipType":"UnlimitedMonthly","startDate":"2026-06-09T00:00:00Z"} }
            try
            {
                var doc = System.Text.Json.JsonDocument.Parse(payload);
                if (!doc.RootElement.TryGetProperty("event", out var ev)) return;
                if (ev.GetString() != "payment_succeeded") return;
                if (!doc.RootElement.TryGetProperty("paymentId", out var pidEl)) return;
                var pid = pidEl.GetString();
                if (string.IsNullOrWhiteSpace(pid)) return;

                if (!_intents.TryGetValue(pid, out var info))
                {
                    _logger.LogWarning("Simulated payment webhook for unknown payment id {Id}", pid);
                    return;
                }

                // Optional metadata from payload overrides stored metadata
                IDictionary<string,string> meta = new Dictionary<string,string>(info.metadata);
                if (doc.RootElement.TryGetProperty("metadata", out var metaEl) && metaEl.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    foreach (var p in metaEl.EnumerateObject()) meta[p.Name] = p.Value.GetString() ?? string.Empty;
                }

                // If membershipType present, activate the membership. This service is a singleton,
                // so resolve the scoped IMembershipService (and its DbContext) from a fresh scope.
                if (meta.TryGetValue("membershipType", out var membershipType) && !string.IsNullOrWhiteSpace(membershipType))
                {
                    if (Enum.TryParse<SportVitaal.Domain.Enums.MembershipType>(membershipType, true, out var mt))
                    {
                        var start = DateTime.UtcNow;
                        if (meta.TryGetValue("startDate", out var sd) && DateTime.TryParse(sd, out var parsed)) start = parsed;
                        var money = new Money(info.amount, info.currency);

                        using var scope = _sp.CreateScope();
                        var membershipService = scope.ServiceProvider.GetRequiredService<IMembershipService>();
                        await membershipService.PurchaseMembershipAsync(info.memberId, mt, start, money, ct);
                        _logger.LogInformation("Simulated payment succeeded and membership activated for member {MemberId}", info.memberId);
                    }
                }
                else
                {
                    _logger.LogInformation("Simulated payment succeeded for member {MemberId} (no membership metadata)", info.memberId);
                }

                // remove intent
                _intents.TryRemove(pid, out _);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing simulated payment webhook payload");
            }
        }
    }
}

