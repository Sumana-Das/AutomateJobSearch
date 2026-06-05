using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;

namespace RecruiterOutreach.Core.Emailing;

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
            IsBodyHtml = Constants.Email.IsBodyHtml
        };

        message.To.Add(new MailAddress(to));

        if (!string.IsNullOrWhiteSpace(attachmentPath))
        {
            if (!File.Exists(attachmentPath))
            {
                _logger.LogWarning(Constants.Email.Logs.AttachmentNotFound, attachmentPath);
            }
            else
            {
                message.Attachments.Add(new Attachment(attachmentPath));
            }
        }

        var password = Environment.GetEnvironmentVariable(Constants.Smtp.Env.GmailAppPasswordKey);
        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogError(Constants.Smtp.Errors.AppPasswordMissingLog);
            throw new InvalidOperationException(Constants.Smtp.Errors.AppPasswordMissing);
        }

        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = _settings.UseSsl,
            Credentials = new NetworkCredential(
                _settings.UserName,
                password)
        };

        _logger.LogInformation(Constants.Email.Logs.Sending, to, subject);

        // SmtpClient does not support CancellationToken directly.
        await client.SendMailAsync(message);

        _logger.LogInformation(Constants.Email.Logs.Sent, to);
    }
}
