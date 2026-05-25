using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RecruiterOutreachConsole.Emailing;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpSettings _settings;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(SmtpSettings settings, ILogger<SmtpEmailSender> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task SendEmailAsync(
        string to,
        string subject,
        string body,
        string? attachmentPath,
        CancellationToken cancellationToken = default)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromAddress),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        message.To.Add(new MailAddress(to));

        if (!string.IsNullOrWhiteSpace(attachmentPath))
        {
            if (!File.Exists(attachmentPath))
            {
                _logger.LogWarning("Attachment file not found at path {AttachmentPath}", attachmentPath);
            }
            else
            {
                message.Attachments.Add(new Attachment(attachmentPath));
            }
        }

        var password = Environment.GetEnvironmentVariable("GMAIL_APP_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogError("GMAIL_APP_PASSWORD environment variable is not set. Cannot authenticate to SMTP server.");
            throw new InvalidOperationException("GMAIL_APP_PASSWORD environment variable is not set.");
        }

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.UseSsl,
            Credentials = new NetworkCredential(
                _settings.UserName,
                password)
        };

        _logger.LogInformation("Sending email to {To} with subject '{Subject}'", to, subject);

        // SmtpClient does not support CancellationToken directly.
        await client.SendMailAsync(message);

        _logger.LogInformation("Email successfully sent to {To}", to);
    }
}
