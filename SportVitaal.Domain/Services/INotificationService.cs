using SportVitaal.Domain.ValueObjects;

namespace SportVitaal.Domain.Services
{
    public interface INotificationService
    {
        Task SendEmailAsync(Email to, string subject, string body, CancellationToken ct = default);
    }
}

