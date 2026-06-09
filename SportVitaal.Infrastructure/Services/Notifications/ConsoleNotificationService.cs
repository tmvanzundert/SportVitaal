using System;
using System.Threading;
using System.Threading.Tasks;
using SportVitaal.Domain.Services;
using SportVitaal.Domain.ValueObjects;

namespace SportVitaal.Infrastructure.Services.Notifications
{
    /// <summary>
    /// Simple console-based notification implementation for development/demo.
    /// Replace with SMTP/SendGrid/Push implementations in production.
    /// </summary>
    public class ConsoleNotificationService : INotificationService
    {
        public Task SendEmailAsync(Email to, string subject, string body, CancellationToken ct = default)
        {
            Console.WriteLine($"[Notification] To: {to}, Subject: {subject}\n{body}");
            return Task.CompletedTask;
        }
    }
}

