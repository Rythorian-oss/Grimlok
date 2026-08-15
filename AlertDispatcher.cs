using Grimlok.Configuration;
using Grimlok.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Grimlok.Services;

public interface IAlertDispatcher
{
    Task DispatchAsync(SecurityAlert alert, CancellationToken cancellationToken = default);
}

public sealed class SmtpAlertDispatcher : IAlertDispatcher
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpAlertDispatcher> _logger;

    public SmtpAlertDispatcher(
        IOptions<GrimlokOptions> options,
        ILogger<SmtpAlertDispatcher> logger)
    {
        var opt = options.Value;
        _options = !string.IsNullOrWhiteSpace(opt.Smtp.Host) ? opt.Smtp : new SmtpOptions
        {
            EmailEnabled = opt.Alerts.EmailEnabled,
            Host = opt.Alerts.SmtpHost,
            Port = opt.Alerts.SmtpPort,
            EnableSsl = opt.Alerts.UseSsl,
            Username = opt.Alerts.Username,
            From = opt.Alerts.From,
            To = opt.Alerts.To,
            PasswordEnvironmentVariable = opt.Alerts.PasswordEnvironmentVariable
        };
        _logger = logger;
    }

    public async Task DispatchAsync(SecurityAlert alert, CancellationToken cancellationToken = default)
    {
        if (_options.EmailEnabled || !string.IsNullOrWhiteSpace(_options.Host))
        {
            ValidateConfiguration();
            var password = !string.IsNullOrWhiteSpace(_options.Password)
                ? _options.Password
                : Environment.GetEnvironmentVariable(_options.PasswordEnvironmentVariable);

            if (!string.IsNullOrWhiteSpace(password))
            {
                var message = new MimeMessage();
                message.From.Add(MailboxAddress.Parse(_options.From));
                message.To.Add(MailboxAddress.Parse(_options.To));
                message.Subject = $"Grimlok motion alert - {alert.DetectedAt:yyyy-MM-dd HH:mm:ss} UTC";

                var body = new BodyBuilder
                {
                    TextBody =
                        $"Motion was detected at {alert.DetectedAt:O}.\n" +
                        $"Motion ratio: {alert.MotionRatio:P2}\n" +
                        $"Detected objects: {string.Join(", ", alert.Objects.Select(item => $"{item.Label} ({item.Confidence:P0})"))}"
                };

                if (alert.SnapshotJpeg is { Length: > 0 })
                {
                    body.Attachments.Add(
                        $"motion-{alert.DetectedAt:yyyyMMdd-HHmmss}.jpg",
                        alert.SnapshotJpeg,
                        new ContentType("image", "jpeg"));
                }

                message.Body = body.ToMessageBody();

                using var client = new SmtpClient();
                try
                {
                    await client.ConnectAsync(
                        _options.Host,
                        _options.Port,
                        _options.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
                        cancellationToken);
                    await client.AuthenticateAsync(_options.Username, password, cancellationToken);
                    await client.SendAsync(message, cancellationToken);
                    _logger.LogInformation("Motion alert email sent to {Recipient}", _options.To);
                }
                finally
                {
                    if (client.IsConnected)
                        await client.DisconnectAsync(true, cancellationToken);
                }
            }
            else
            {
                throw new InvalidOperationException(
                    $"Email is enabled but password is not provided and {_options.PasswordEnvironmentVariable} is not set");
            }
        }
        else
        {
            _logger.LogInformation("Motion alert raised; email dispatch is disabled or host not configured");
            return;
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.Host) ||
            string.IsNullOrWhiteSpace(_options.Username) ||
            string.IsNullOrWhiteSpace(_options.From) ||
            string.IsNullOrWhiteSpace(_options.To))
        {
            throw new InvalidOperationException(
                "Email alerts require Host/SmtpHost, Username, From, and To settings");
        }
    }
}