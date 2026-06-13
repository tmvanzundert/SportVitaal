using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SportVitaal.Domain.DomainEvents;
using SportVitaal.Infrastructure.Data;

namespace SportVitaal.Infrastructure.HostedServices
{
    /// <summary>
    /// Hosted service that scans for memberships that will expire in ~6 weeks and raises domain events.
    /// Runs once per day.
    /// </summary>
    public class MembershipExpiryHostedService : IHostedService, IDisposable
    {
        // The hosted service is a singleton, so scoped services (AppDbContext, the dispatcher)
        // must be resolved from a per-iteration scope rather than injected directly.
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MembershipExpiryHostedService> _logger;
        private CancellationTokenSource? _cts;
        private Task? _executingTask;

        public MembershipExpiryHostedService(IServiceScopeFactory scopeFactory, ILogger<MembershipExpiryHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("MembershipExpiryHostedService starting.");
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executingTask = Task.Run(() => ExecuteAsync(_cts.Token));
            return Task.CompletedTask;
        }

        private async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var target = DateTime.UtcNow.Date.AddDays(42); // ~6 weeks from now
                    var nextDay = target.AddDays(1);

                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();

                    var users = await db.Users
                        .AsNoTracking()
                        .Where(u => u.Membership != null && u.Membership.EndDate != null && u.Membership.EndDate >= target && u.Membership.EndDate < nextDay)
                        .ToListAsync(stoppingToken);

                    foreach (var user in users)
                    {
                        var ev = new MembershipExpiringSoonEvent(Guid.Empty, user.Id, user.Membership!.EndDate!.Value);
                        await dispatcher.DispatchAsync(ev);
                        _logger.LogInformation("Dispatched MembershipExpiringSoonEvent for user {UserId}", user.Id);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break; // shutting down
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while scanning memberships for expiry.");
                }

                // Wait 24 hours or until cancellation
                try
                {
                    await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    // ignore
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("MembershipExpiryHostedService stopping.");
            if (_executingTask == null)
                return;

            _cts?.Cancel();
            await Task.WhenAny(_executingTask, Task.Delay(Timeout.Infinite, cancellationToken));
        }

        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}


