using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RecruiterOutreach.Core.Emailing;
using RecruiterOutreach.Core.Gemini;

namespace RecruiterOutreach.Core.Outreach;

public sealed class OutreachService
{
    private readonly OutreachSettings _settings;
    private readonly IEmailSender _emailSender;
    private readonly IGeminiPersonalizationService _gemini;
    private readonly ILogger<OutreachService> _logger;

    public OutreachService(
        OutreachSettings settings,
        IEmailSender emailSender,
        IGeminiPersonalizationService gemini,
        ILogger<OutreachService> logger)
    {
        _settings = settings;
        _emailSender = emailSender;
        _gemini = gemini;
        _logger = logger;
    }

    public async Task RunAsync(
        string company,
        string role,
        string? jobDescription,
        string? roleKey = null,
        string? templateKind = null,
        IReadOnlyCollection<string>? recruiterEmails = null,
        string? subjectOverride = null,
        string? emailBodyOverride = null,
        string? resumeAttachmentPath = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting outreach run for Company={Company}, Role={Role}", company, role);

        if (!string.IsNullOrWhiteSpace(jobDescription))
        {
            _logger.LogInformation("JD provided. Running in DRAFT mode (no emails will be sent).");

            var personalization = await _gemini.PersonalizeAsync(
                string.Empty,
                jobDescription,
                cancellationToken);

            // Put suggestions next to the default attachment, or next to the executable if not set.
            var baseFolder = !string.IsNullOrWhiteSpace(_settings.Resume.DefaultAttachmentPath)
                ? Path.GetDirectoryName(_settings.Resume.DefaultAttachmentPath) ?? Directory.GetCurrentDirectory()
                : Path.GetDirectoryName(Environment.ProcessPath!) ?? Directory.GetCurrentDirectory();

            var outputFolder = Path.Combine(baseFolder, "Suggestions");
            Directory.CreateDirectory(outputFolder);

            var safeCompany = MakeSafeFileNamePart(company);
            var safeRole = MakeSafeFileNamePart(role);
            var fileName = $"ResumeSuggestions_{safeCompany}_{safeRole}_{DateTime.Now:yyyyMMddHHmmss}.txt";
            var fullPath = Path.Combine(outputFolder, fileName);

            using (var writer = new StreamWriter(fullPath))
            {
                await writer.WriteLineAsync($"Company: {company}");
                await writer.WriteLineAsync($"Role: {role}");
                await writer.WriteLineAsync();

                await writer.WriteLineAsync("Our scoring (heuristic overlap using Gemini JD/resume keywords):");
                await writer.WriteLineAsync($"Match score: {personalization.OurMatchScore}");
                await writer.WriteLineAsync();

                if (personalization.OurKeywordsToAdd.Count > 0)
                {
                    await writer.WriteLineAsync("Keywords to consider adding:");
                    foreach (var kw in personalization.OurKeywordsToAdd)
                    {
                        await writer.WriteLineAsync("- " + kw);
                    }

                    await writer.WriteLineAsync();
                }

                if (personalization.OurMissingKeywords.Count > 0)
                {
                    await writer.WriteLineAsync("Keywords currently missing from your resume:");
                    foreach (var kw in personalization.OurMissingKeywords)
                    {
                        await writer.WriteLineAsync("- " + kw);
                    }

                    await writer.WriteLineAsync();
                }

                await writer.WriteLineAsync("Gemini scoring (direct from model):");
                await writer.WriteLineAsync($"Match score: {personalization.GeminiMatchScore}");
                await writer.WriteLineAsync();

                if (personalization.GeminiKeywordsToAdd.Count > 0)
                {
                    await writer.WriteLineAsync("Keywords to consider adding (Gemini):");
                    foreach (var kw in personalization.GeminiKeywordsToAdd)
                    {
                        await writer.WriteLineAsync("- " + kw);
                    }

                    await writer.WriteLineAsync();
                }

                if (personalization.GeminiMissingKeywords.Count > 0)
                {
                    await writer.WriteLineAsync("Keywords currently missing (Gemini view):");
                    foreach (var kw in personalization.GeminiMissingKeywords)
                    {
                        await writer.WriteLineAsync("- " + kw);
                    }

                    await writer.WriteLineAsync();
                }

                await writer.WriteLineAsync("Detailed suggestions:");
                await writer.WriteLineAsync();
                await writer.WriteAsync(personalization.UpdatedResumeText);
            }

            _logger.LogInformation("Resume suggestions file created at {Path}. Review and manually update your DOCX/PDF.", fullPath);

            return;
        }

        var recipients = recruiterEmails is null
            ? Array.Empty<string>()
            : recruiterEmails
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        if (recipients.Length == 0)
        {
            _logger.LogWarning("No recruiter email addresses provided. Aborting outreach run.");
            return;
        }

        var resumePath = !string.IsNullOrWhiteSpace(resumeAttachmentPath)
            ? resumeAttachmentPath
            : _settings.Resume.DefaultAttachmentPath;
        if (!string.IsNullOrWhiteSpace(resumePath) && !File.Exists(resumePath))
        {
            _logger.LogWarning("Configured default resume attachment does not exist at {Path}", resumePath);
        }

        // Resolve the template variant to use (e.g., HR vs Referral for the given role)
        var template = ResolveTemplate(roleKey, templateKind);

        foreach (var recruiterEmail in recipients)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var subjectTemplate = !string.IsNullOrWhiteSpace(subjectOverride)
                ? subjectOverride
                : template.SubjectTemplate;

            var bodyTemplate = !string.IsNullOrWhiteSpace(emailBodyOverride)
                ? emailBodyOverride
                : template.BodyTemplate;

            var recruiterName = GetRecruiterNameFromEmail(recruiterEmail, _settings);

            var subject = subjectTemplate
                .Replace("{Company}", company, StringComparison.OrdinalIgnoreCase)
                .Replace("{Role}", role, StringComparison.OrdinalIgnoreCase)
                .Replace("{RecruiterName}", recruiterName, StringComparison.OrdinalIgnoreCase);

            var body = bodyTemplate
                .Replace("{Company}", company, StringComparison.OrdinalIgnoreCase)
                .Replace("{Role}", role, StringComparison.OrdinalIgnoreCase)
                .Replace("{RecruiterName}", recruiterName, StringComparison.OrdinalIgnoreCase);

            _logger.LogInformation("Sending outreach email to {Recruiter}", recruiterEmail);

            try
            {
                await _emailSender.SendEmailAsync(
                    recruiterEmail,
                    subject,
                    body,
                    resumePath,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Recruiter}", recruiterEmail);
            }
        }

        _logger.LogInformation("Outreach run completed.");
    }

    private static string GetRecruiterNameFromEmail(string email, OutreachSettings settings)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "Recruiter";
        }

        var atIndex = email.IndexOf('@');
        var localPart = atIndex > 0 ? email[..atIndex] : email;

        var lower = localPart.ToLowerInvariant();

        // Use only the configured generic recruiter keywords. If none are configured,
        // we treat all addresses as personal inboxes.
        var keywords = settings.GenericRecruiterKeywords ?? new List<string>();

        foreach (var keyword in keywords)
        {
            if (!string.IsNullOrWhiteSpace(keyword) && lower.Contains(keyword.ToLowerInvariant()))
            {
                return "Recruiter";
            }
        }

        // Split local part on non-letter characters and take the first token
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (var ch in localPart)
        {
            if (char.IsLetter(ch))
            {
                current.Append(ch);
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        var firstToken = tokens.Count > 0 ? tokens[0] : localPart;
        if (string.IsNullOrWhiteSpace(firstToken))
        {
            return "Recruiter";
        }

        // Capitalize first letter, lower the rest (e.g., "anmolika" -> "Anmolika")
        var trimmed = firstToken.Trim();
        if (trimmed.Length == 1)
        {
            return trimmed.ToUpperInvariant();
        }

        return char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant();
    }

    private RoleTemplateVariant ResolveTemplate(string? roleKey, string? templateKind)
    {
        // Normalise kind (default to HR if not specified)
        var kind = string.IsNullOrWhiteSpace(templateKind) ? "Hr" : templateKind;

        // Helper local function to extract variant from a role template
        static RoleTemplateVariant? GetVariant(RoleEmailTemplateSettings roleTemplate, string kindValue)
        {
            return kindValue.Equals("Referral", StringComparison.OrdinalIgnoreCase)
                ? roleTemplate.Templates.Referral
                : roleTemplate.Templates.Hr;
        }

        // 1) Try explicit roleKey + kind
        if (!string.IsNullOrWhiteSpace(roleKey) && _settings.RoleEmailTemplates.Count > 0)
        {
            var match = _settings.RoleEmailTemplates.Find(t => string.Equals(t.Key, roleKey, StringComparison.OrdinalIgnoreCase));
            var variant = match is not null ? GetVariant(match, kind) : null;
            if (variant is not null)
            {
                return variant;
            }
        }

        // 2) Try default role + kind
        if (!string.IsNullOrWhiteSpace(_settings.DefaultRoleKey) && _settings.RoleEmailTemplates.Count > 0)
        {
            var defaultMatch = _settings.RoleEmailTemplates.Find(t => string.Equals(t.Key, _settings.DefaultRoleKey, StringComparison.OrdinalIgnoreCase));
            var variant = defaultMatch is not null ? GetVariant(defaultMatch, kind) : null;
            if (variant is not null)
            {
                return variant;
            }
        }

        // 3) Fallback: first available role + HR (or Referral) variant
        foreach (var t in _settings.RoleEmailTemplates)
        {
            var variant = GetVariant(t, kind) ?? GetVariant(t, "Hr") ?? GetVariant(t, "Referral");
            if (variant is not null)
            {
                return variant;
            }
        }

        // 4) Absolute fallback: empty template to avoid nulls
        return new RoleTemplateVariant();
    }

    private static string MakeSafeFileNamePart(string input)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            input = input.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(input) ? "Unknown" : input;
    }
}
