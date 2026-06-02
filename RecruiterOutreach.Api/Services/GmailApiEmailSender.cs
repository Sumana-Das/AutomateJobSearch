using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using System.Net.Mime;
using System.Text;
using RecruiterOutreach.Core.Emailing;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace RecruiterOutreach.Api.Services;

public sealed class GmailApiEmailSender : IEmailSender
{
    private readonly InMemoryTokenStore _tokens;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GmailApiEmailSender(InMemoryTokenStore tokens, IHttpContextAccessor httpContextAccessor)
    {
        _tokens = tokens;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task SendEmailAsync(
        string to,
        string subject,
        string body,
        string? attachmentPath,
        CancellationToken cancellationToken = default)
    {
        var senderEmail = ResolveCurrentUserEmail();
        if (string.IsNullOrWhiteSpace(senderEmail))
        {
            throw new InvalidOperationException("No signed-in user found. Please sign in with Google to send email.");
        }

        if (!_tokens.TryGet(senderEmail, out var accessToken, out var refreshToken))
        {
            throw new InvalidOperationException("No OAuth tokens found for the current user. Please sign in again.");
        }

        var initializer = new BaseClientService.Initializer
        {
            HttpClientInitializer = GoogleCredential.FromAccessToken(accessToken),
            ApplicationName = "TailorMailer AI",
        };

        using var gmail = new GmailService(initializer);

        // Build raw RFC822 message
        var raw = BuildRawMessage(senderEmail, to, subject, body, attachmentPath);
        var message = new Message { Raw = raw };

        var request = gmail.Users.Messages.Send(message, "me");
        await request.ExecuteAsync(cancellationToken);
    }

    private string ResolveCurrentUserEmail()
    {
        var http = _httpContextAccessor.HttpContext;
        if (http is null) return string.Empty;
        if (!http.Request.Cookies.TryGetValue("tm_user", out var payload) || string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("email", out var e) ? (e.GetString() ?? string.Empty) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string BuildRawMessage(string from, string to, string subject, string body, string? attachmentPath)
    {
        var hasAttachment = !string.IsNullOrWhiteSpace(attachmentPath) && File.Exists(attachmentPath!);

        string rawText;
        if (!hasAttachment)
        {
            // Simple text/plain message
            var sb = new StringBuilder();
            sb.AppendLine($"From: {from}");
            sb.AppendLine($"To: {to}");
            sb.AppendLine($"Subject: {subject}");
            sb.AppendLine("MIME-Version: 1.0");
            sb.AppendLine("Content-Type: text/plain; charset=\"utf-8\"");
            sb.AppendLine("Content-Transfer-Encoding: 7bit");
            sb.AppendLine();
            sb.AppendLine(body ?? string.Empty);
            rawText = sb.ToString();
        }
        else
        {
            var boundary = "===============TAILORMAILER_" + Guid.NewGuid().ToString("N");
            var fileName = Path.GetFileName(attachmentPath!);
            var mimeType = GuessMimeType(fileName);
            var fileBytes = File.ReadAllBytes(attachmentPath!);
            var fileBase64 = Convert.ToBase64String(fileBytes);

            var sb = new StringBuilder();
            sb.AppendLine($"From: {from}");
            sb.AppendLine($"To: {to}");
            sb.AppendLine($"Subject: {subject}");
            sb.AppendLine("MIME-Version: 1.0");
            sb.AppendLine($"Content-Type: multipart/mixed; boundary=\"{boundary}\"");
            sb.AppendLine();

            // Text part
            sb.AppendLine($"--{boundary}");
            sb.AppendLine("Content-Type: text/plain; charset=\"utf-8\"");
            sb.AppendLine("Content-Transfer-Encoding: 7bit");
            sb.AppendLine();
            sb.AppendLine(body ?? string.Empty);
            sb.AppendLine();

            // Attachment part
            sb.AppendLine($"--{boundary}");
            sb.AppendLine($"Content-Type: {mimeType}; name=\"{fileName}\"");
            sb.AppendLine("Content-Transfer-Encoding: base64");
            sb.AppendLine($"Content-Disposition: attachment; filename=\"{fileName}\"");
            sb.AppendLine();

            // Optionally wrap lines to 76 chars per RFC; Gmail accepts unwrapped but we wrap for safety
            const int wrap = 76;
            for (int i = 0; i < fileBase64.Length; i += wrap)
            {
                var len = Math.Min(wrap, fileBase64.Length - i);
                sb.AppendLine(fileBase64.Substring(i, len));
            }

            sb.AppendLine();
            sb.AppendLine($"--{boundary}--");
            sb.AppendLine();

            rawText = sb.ToString();
        }

        // Gmail API expects base64url encoded raw message
        var bytesAll = Encoding.UTF8.GetBytes(rawText);
        return Convert.ToBase64String(bytesAll)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string GuessMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
    }
}
