using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RecruiterOutreach.Core.Emailing;
using RecruiterOutreach.Core.Gemini;
using Microsoft.Extensions.DependencyInjection;

namespace RecruiterOutreach.Core.Outreach;

public sealed class OutreachService
{
    private readonly OutreachSettings _settings;
    private readonly IEmailSender _emailSender;
    private readonly IServiceProvider _services;
    private readonly ILogger<OutreachService> _logger;

    public OutreachService(
        OutreachSettings settings,
        IEmailSender emailSender,
        ILogger<OutreachService> logger,
        IServiceProvider services)
    {
        _settings = settings;
        _emailSender = emailSender;
        _logger = logger;
        _services = services;
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
        _logger.LogInformation(Constants.Logs.Outreach.StartingRun, company, role);

        if (!string.IsNullOrWhiteSpace(jobDescription))
        {
            _logger.LogInformation(Constants.Logs.Outreach.JDDraftMode);

            var gemini = _services.GetService<IGeminiPersonalizationService>();
            if (gemini is null)
            {
                throw new InvalidOperationException(Constants.Gemini.Errors.MissingConfig);
            }

            var personalization = await gemini.ResumeSuggestionAsync(
                string.Empty,
                jobDescription,
                null,
                cancellationToken);

            // Put suggestions next to the default attachment, or next to the executable if not set.
            var baseFolder = !string.IsNullOrWhiteSpace(_settings.Resume.DefaultAttachmentPath)
                ? Path.GetDirectoryName(_settings.Resume.DefaultAttachmentPath) ?? Directory.GetCurrentDirectory()
                : Path.GetDirectoryName(Environment.ProcessPath!) ?? Directory.GetCurrentDirectory();

            var outputFolder = Path.Combine(baseFolder, Constants.Files.SuggestionsDir);
            Directory.CreateDirectory(outputFolder);

            var safeCompany = MakeSafeFileNamePart(company);
            var safeRole = MakeSafeFileNamePart(role);
            var fileName = $"{Constants.Files.SuggestionsFilePrefix}{safeCompany}_{safeRole}_{DateTime.Now}:{Constants.Files.SuggestionsFileTimestampFormat}{Constants.Files.SuggestionsFileExtension}";
            var fullPath = Path.Combine(outputFolder, fileName);

            using (var writer = new StreamWriter(fullPath))
            {
                await writer.WriteLineAsync($"{Constants.Labels.Company}{company}");
                await writer.WriteLineAsync($"{Constants.Labels.Role}{role}");
                await writer.WriteLineAsync();

                await writer.WriteLineAsync(Constants.Labels.OurScoringHeader);
                await writer.WriteLineAsync($"{Constants.Labels.MatchScore}{personalization.OurMatchScore}");
                await writer.WriteLineAsync();

                if (personalization.OurKeywordsToAdd.Count > 0)
                {
                    await writer.WriteLineAsync(Constants.Labels.KeywordsToAdd);
                    foreach (var kw in personalization.OurKeywordsToAdd)
                    {
                        await writer.WriteLineAsync(Constants.Labels.BulletPrefix + kw);
                    }

                    await writer.WriteLineAsync();
                }

                if (personalization.OurMissingKeywords.Count > 0)
                {
                    await writer.WriteLineAsync(Constants.Labels.MissingKeywords);
                    foreach (var kw in personalization.OurMissingKeywords)
                    {
                        await writer.WriteLineAsync(Constants.Labels.BulletPrefix + kw);
                    }

                    await writer.WriteLineAsync();
                }

                await writer.WriteLineAsync(Constants.Labels.GeminiScoringHeader);
                await writer.WriteLineAsync($"{Constants.Labels.MatchScore}{personalization.GeminiMatchScore}");
                await writer.WriteLineAsync();

                if (personalization.GeminiKeywordsToAdd.Count > 0)
                {
                    await writer.WriteLineAsync(Constants.Labels.KeywordsToAddGemini);
                    foreach (var kw in personalization.GeminiKeywordsToAdd)
                    {
                        await writer.WriteLineAsync(Constants.Labels.BulletPrefix + kw);
                    }

                    await writer.WriteLineAsync();
                }

                if (personalization.GeminiMissingKeywords.Count > 0)
                {
                    await writer.WriteLineAsync(Constants.Labels.MissingKeywordsGemini);
                    foreach (var kw in personalization.GeminiMissingKeywords)
                    {
                        await writer.WriteLineAsync(Constants.Labels.BulletPrefix + kw);
                    }

                    await writer.WriteLineAsync();
                }

                await writer.WriteLineAsync(Constants.Labels.DetailedSuggestions);
                await writer.WriteLineAsync();
                await writer.WriteAsync(personalization.UpdatedResumeText);
            }

            _logger.LogInformation(Constants.Logs.Outreach.SuggestionsCreatedAt, fullPath);

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
            _logger.LogWarning(Constants.Logs.Outreach.NoRecruiters);
            return;
        }

        var resumePath = !string.IsNullOrWhiteSpace(resumeAttachmentPath)
            ? resumeAttachmentPath
            : _settings.Resume.DefaultAttachmentPath;
        if (!string.IsNullOrWhiteSpace(resumePath) && !File.Exists(resumePath))
        {
            _logger.LogWarning(Constants.Logs.Outreach.DefaultResumeMissing, resumePath);
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
                .Replace(Constants.Placeholders.Company, company, StringComparison.OrdinalIgnoreCase)
                .Replace(Constants.Placeholders.Role, role, StringComparison.OrdinalIgnoreCase)
                .Replace(Constants.Placeholders.RecruiterName, recruiterName, StringComparison.OrdinalIgnoreCase);

            var body = bodyTemplate
                .Replace(Constants.Placeholders.Company, company, StringComparison.OrdinalIgnoreCase)
                .Replace(Constants.Placeholders.Role, role, StringComparison.OrdinalIgnoreCase)
                .Replace(Constants.Placeholders.RecruiterName, recruiterName, StringComparison.OrdinalIgnoreCase);

            _logger.LogInformation(Constants.Logs.Outreach.SendingTo, recruiterEmail);

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
                _logger.LogError(ex, Constants.Logs.Outreach.SendFailed, recruiterEmail);
            }
        }

        _logger.LogInformation(Constants.Logs.Outreach.Completed);
    }

    private static string GetRecruiterNameFromEmail(string email, OutreachSettings settings)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Constants.Defaults.RecruiterName;
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
        var kind = string.IsNullOrWhiteSpace(templateKind) ? Constants.TemplateKinds.Hr : templateKind;

        // Helper local function to extract variant from a role template
        static RoleTemplateVariant? GetVariant(RoleEmailTemplateSettings roleTemplate, string kindValue)
        {
            return kindValue.Equals(Constants.TemplateKinds.Referral, StringComparison.OrdinalIgnoreCase)
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
            var variant = GetVariant(t, kind) ?? GetVariant(t, Constants.TemplateKinds.Hr) ?? GetVariant(t, Constants.TemplateKinds.Referral);
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
