using Microsoft.EntityFrameworkCore;
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
        private readonly AppDbContext _db;
        private readonly IDomainEventDispatcher _dispatcher;
        private readonly ILogger<MembershipExpiryHostedService> _logger;
        private CancellationTokenSource? _cts;
        private Task? _executingTask;

        public MembershipExpiryHostedService(AppDbContext db, IDomainEventDispatcher dispatcher, ILogger<MembershipExpiryHostedService> logger)
        {
            _db = db;
            _dispatcher = dispatcher;
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

                    var users = await _db.Users
                        .AsNoTracking()
                        .Where(u => u.Membership != null && u.Membership.EndDate != null && u.Membership.EndDate >= target && u.Membership.EndDate < nextDay)
                        .ToListAsync(stoppingToken);

                    foreach (var user in users)
                    {
                        var ev = new MembershipExpiringSoonEvent(Guid.Empty, user.Id, user.Membership!.EndDate!.Value);
                        await _dispatcher.DispatchAsync(ev);
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


