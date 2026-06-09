using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using SportVitaal.Domain.Services;
using SportVitaal.Domain.ValueObjects;

namespace SportVitaal.Infrastructure.Services.Notifications
{
    public class SmtpOptions
    {
        public string? Host { get; set; }
        public int Port { get; set; } = 25;
        public bool UseSsl { get; set; } = false;
        public string? User { get; set; }
        public string? Password { get; set; }
        public string? FromAddress { get; set; }
        public string? FromName { get; set; }
    }

    public class SmtpEmailSender : INotificationService
    {
        private readonly SmtpOptions _options;
        private readonly ILogger<SmtpEmailSender> _logger;

        public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task SendEmailAsync(Email to, string subject, string body, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_options.Host))
            {
                _logger.LogWarning("SMTP host not configured. Falling back to console output.");
                Console.WriteLine($"[Email to {to}] {subject}\n{body}");
                return;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_options.FromName ?? "SportVitaal", _options.FromAddress ?? "noreply@sportvitaal.local"));
            message.To.Add(MailboxAddress.Parse(to.Address));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            await client.ConnectAsync(_options.Host, _options.Port, _options.UseSsl, ct);
            if (!string.IsNullOrWhiteSpace(_options.User) && !string.IsNullOrWhiteSpace(_options.Password))
            {
                await client.AuthenticateAsync(_options.User, _options.Password, ct);
            }
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
    }
}

