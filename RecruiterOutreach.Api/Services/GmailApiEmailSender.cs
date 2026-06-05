using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using System.Net.Mime;
using System.Text;
using RecruiterOutreach.Core.Emailing;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using RecruiterOutreach.Api;

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
            throw new InvalidOperationException(Constants.Responses.NoSignedInUser);
        }

        if (!_tokens.TryGet(senderEmail, out var accessToken, out var refreshToken))
        {
            throw new InvalidOperationException(Constants.Responses.NoOAuthTokensForUser);
        }

        var initializer = new BaseClientService.Initializer
        {
            HttpClientInitializer = GoogleCredential.FromAccessToken(accessToken),
            ApplicationName = Constants.Defaults.ApplicationName,
        };

        using var gmail = new GmailService(initializer);

        // Build raw RFC822 message
        var raw = BuildRawMessage(senderEmail, to, subject, body, attachmentPath);
        var message = new Message { Raw = raw };

        var request = gmail.Users.Messages.Send(message, Constants.Defaults.Me);
        await request.ExecuteAsync(cancellationToken);
    }

    private string ResolveCurrentUserEmail()
    {
        var http = _httpContextAccessor.HttpContext;
        if (http is null) return string.Empty;
        if (!http.Request.Cookies.TryGetValue(Constants.Defaults.CookieUserName, out var payload) || string.IsNullOrWhiteSpace(payload))
        {
            return string.Empty;
        }
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(Constants.Defaults.JsonPropEmail, out var e) ? (e.GetString() ?? string.Empty) : string.Empty;
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
            rawText = Constants.Email.BuildPlainTextEmail(from, to, subject, body);
        }
        else
        {
            var boundary = Constants.Defaults.BoundaryPrefix + Guid.NewGuid().ToString("N");
            var fileName = Path.GetFileName(attachmentPath!);
            var mimeType = GuessMimeType(fileName);
            var fileBytes = File.ReadAllBytes(attachmentPath!);
            var fileBase64 = Convert.ToBase64String(fileBytes);

            rawText = Constants.Email.BuildMultipartMixedEmail(from, to, subject, body, boundary, fileName, mimeType, fileBase64, Constants.Defaults.Base64WrapColumns);
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
            ".pdf" => Constants.Email.ApplicationPdf,
            ".txt" => Constants.Email.TextPlain,
            ".doc" => Constants.Email.ApplicationMsWord,
            ".docx" => Constants.Email.ApplicationDocx,
            ".png" => Constants.Email.ImagePng,
            ".jpg" or ".jpeg" => Constants.Email.ImageJpeg,
            ".gif" => Constants.Email.ImageGif,
            _ => Constants.Email.ApplicationOctetStream
        };
    }
}
