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

                if (personalization.KeywordsToAdd.Count > 0)
                {
                    await writer.WriteLineAsync("Keywords to consider adding to your resume:");
                    foreach (var kw in personalization.KeywordsToAdd)
                    {
                        await writer.WriteLineAsync("- " + kw);
                    }

                    await writer.WriteLineAsync();
                }

                if (personalization.KeywordsToRemove.Count > 0)
                {
                    await writer.WriteLineAsync("Keywords to consider removing or de-emphasizing:");
                    foreach (var kw in personalization.KeywordsToRemove)
                    {
                        await writer.WriteLineAsync("- " + kw);
                    }

                    await writer.WriteLineAsync();
                }

                await writer.WriteLineAsync("Detailed suggestions and JD excerpt:");
                await writer.WriteLineAsync();
                await writer.WriteAsync(personalization.UpdatedResumeText);
            }

            _logger.LogInformation("Resume suggestions file created at {Path}. Review and manually update your DOCX/PDF.", fullPath);

            return;
        }

        if (_settings.Recruiters.Count == 0)
        {
            _logger.LogWarning("No recruiters configured. Aborting outreach run.");
            return;
        }

        var resumePath = _settings.Resume.DefaultAttachmentPath;
        if (!string.IsNullOrWhiteSpace(resumePath) && !File.Exists(resumePath))
        {
            _logger.LogWarning("Configured default resume attachment does not exist at {Path}", resumePath);
        }

        foreach (var recruiterEmail in _settings.Recruiters)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var subject = _settings.EmailTemplate.SubjectTemplate
                .Replace("{Company}", company, StringComparison.OrdinalIgnoreCase)
                .Replace("{Role}", role, StringComparison.OrdinalIgnoreCase);

            var body = _settings.EmailTemplate.BodyTemplate
                .Replace("{Company}", company, StringComparison.OrdinalIgnoreCase)
                .Replace("{Role}", role, StringComparison.OrdinalIgnoreCase);

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

    private static string MakeSafeFileNamePart(string input)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            input = input.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(input) ? "Unknown" : input;
    }
}
