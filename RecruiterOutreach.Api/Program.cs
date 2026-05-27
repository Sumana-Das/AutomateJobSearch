using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Antiforgery;
using UglyToad.PdfPig;
using RecruiterOutreach.Api.Contracts;
using RecruiterOutreach.Core;
using RecruiterOutreach.Core.Emailing;
using RecruiterOutreach.Core.Gemini;
using RecruiterOutreach.Core.Outreach;

var builder = WebApplication.CreateBuilder(args);

// Load OutreachSettings from the named section in configuration
var outreachSettings = builder.Configuration
    .GetSection("OutreachSettings")
    .Get<OutreachSettings>() ?? new OutreachSettings();

// Load role email templates from JSON files (if present)
var templatesFolder = Path.Combine(AppContext.BaseDirectory, "EmailTemplates");
if (Directory.Exists(templatesFolder))
{
    foreach (var file in Directory.GetFiles(templatesFolder, "*.json", SearchOption.TopDirectoryOnly))
    {
        try
        {
            var json = File.ReadAllText(file);
            var template = JsonSerializer.Deserialize<RoleEmailTemplateSettings>(json);
            if (template is not null && !string.IsNullOrWhiteSpace(template.Key))
            {
                outreachSettings.RoleEmailTemplates.RemoveAll(t => string.Equals(t.Key, template.Key, StringComparison.OrdinalIgnoreCase));
                outreachSettings.RoleEmailTemplates.Add(template);
            }
        }
        catch
        {
            // Ignore malformed template files; they can be fixed or removed without breaking startup.
        }
    }
}

// Register core settings and services
builder.Services.AddSingleton(outreachSettings);
builder.Services.AddSingleton(outreachSettings.SmtpSettings);
builder.Services.AddSingleton(outreachSettings.Gemini);

builder.Services.AddLogging();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<IGeminiPersonalizationService, GeminiPersonalizationService>();
builder.Services.AddSingleton<OutreachService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Simple status endpoint so UI can see whether Gmail/Gemini are configured
app.MapGet("/api/status", (OutreachSettings settings) =>
{
    var gmailPassword = Environment.GetEnvironmentVariable("GMAIL_APP_PASSWORD");
    var geminiApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

    var response = new StatusResponse(
        GmailConfigured: !string.IsNullOrWhiteSpace(gmailPassword),
        GeminiConfigured: !string.IsNullOrWhiteSpace(geminiApiKey) || !string.IsNullOrWhiteSpace(settings.Gemini?.ApiKey),
        SmtpUserName: settings.SmtpSettings?.UserName);

    return Results.Ok(response);
})
.WithName("GetStatus");

// Setup endpoint to allow providing Gmail app password and Gemini API key at runtime.
// NOTE: These are applied for the current process only; for persistence across restarts,
// users should still configure real environment variables or secrets.
app.MapPost("/api/setup", ([FromBody] SetupRequest request, OutreachSettings settings) =>
{
    if (!string.IsNullOrWhiteSpace(request.GmailAppPassword))
    {
        Environment.SetEnvironmentVariable("GMAIL_APP_PASSWORD", request.GmailAppPassword);
    }

    if (!string.IsNullOrWhiteSpace(request.GeminiApiKey))
    {
        Environment.SetEnvironmentVariable("GEMINI_API_KEY", request.GeminiApiKey);
        if (settings.Gemini is not null)
        {
            settings.Gemini.ApiKey = request.GeminiApiKey;
        }
    }

    return Results.Ok();
})
.WithName("SetupConfiguration");

// Templates endpoint (for UI to drive dropdown from config)
app.MapGet("/api/templates", (OutreachSettings settings) =>
{
    var roleTemplates = settings.RoleEmailTemplates ?? new List<RoleEmailTemplateSettings>();

    var roles = roleTemplates
        .Select(r => new
        {
            r.Key,
            r.DisplayName,
            AvailableTemplates = (r.Templates is null
                ? Array.Empty<string>()
                : new[]
                {
                    r.Templates.Hr is not null ? "Hr" : null,
                    r.Templates.Referral is not null ? "Referral" : null
                }.Where(x => x is not null)),
            Templates = r.Templates is null
                ? null
                : new
                {
                    hr = r.Templates.Hr is not null
                        ? new { r.Templates.Hr.SubjectTemplate, r.Templates.Hr.BodyTemplate }
                        : null,
                    referral = r.Templates.Referral is not null
                        ? new { r.Templates.Referral.SubjectTemplate, r.Templates.Referral.BodyTemplate }
                        : null
                }
        });

    return Results.Ok(roles);
})
.WithName("GetTemplates");

// Template preview endpoint (for UI to pre-populate subject/body from JSON templates)
app.MapGet("/api/templates/preview", ([FromQuery] string roleKey, [FromQuery] string templateKind, OutreachSettings settings) =>
{
    var role = settings.RoleEmailTemplates
        .FirstOrDefault(r => string.Equals(r.Key, roleKey, StringComparison.OrdinalIgnoreCase));

    if (role is null)
    {
        return Results.NotFound();
    }

    var variant = templateKind.Equals("Referral", StringComparison.OrdinalIgnoreCase)
        ? role.Templates.Referral
        : role.Templates.Hr;

    if (variant is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(new
    {
        variant.SubjectTemplate,
        variant.BodyTemplate
    });
})
.WithName("GetTemplatePreview");

// Suggestions endpoint (JD-based, with optional resume upload/persistence)
app.MapPost("/api/outreach/suggestions", async (
    [FromForm] string jobDescription,
    [FromForm] bool persistResume,
    IFormFile? resume,
    OutreachSettings settings,
    IGeminiPersonalizationService gemini,
    CancellationToken cancellationToken) =>
{
    // Determine the effective resume text: uploaded file (optionally persisted) or existing default file.
    string resumeText = string.Empty;

    // 1) If a resume file was uploaded with this request, read it and optionally persist it.
    if (resume is not null && resume.Length > 0)
    {
        resumeText = await ExtractResumeTextFromUploadAsync(resume, cancellationToken);

        if (persistResume)
        {
            var targetPath = settings.Resume?.DefaultAttachmentPath;
            if (!string.IsNullOrWhiteSpace(targetPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                using var fileStream = File.Create(targetPath);
                await resume.CopyToAsync(fileStream, cancellationToken);
            }
        }
    }
    else
    {
        // 2) No upload in this request. Fall back to existing configured resume file if present.
        var defaultPath = settings.Resume?.DefaultAttachmentPath;
        if (!string.IsNullOrWhiteSpace(defaultPath) && File.Exists(defaultPath))
        {
            resumeText = await ExtractResumeTextFromFileAsync(defaultPath, cancellationToken);
        }
    }

    var personalization = await gemini.PersonalizeAsync(resumeText, jobDescription, cancellationToken);

    var jdExcerpt = jobDescription.Length > 1000
        ? jobDescription.Substring(0, 1000) + "..."
        : jobDescription;

    var ourScoring = new ScoringView(
        personalization.OurKeywordsToAdd,
        personalization.OurMissingKeywords,
        personalization.OurMatchScore);

    var geminiScoring = new ScoringView(
        personalization.GeminiKeywordsToAdd,
        personalization.GeminiMissingKeywords,
        personalization.GeminiMatchScore);

    var response = new SuggestionsResponse(
        personalization.UpdatedResumeText,
        ourScoring,
        geminiScoring,
        jdExcerpt);

    return Results.Ok(response);
})
.DisableAntiforgery()
.WithName("GetSuggestions");

// Basic send endpoint (no JD-based personalization; uses default attachment unless an override is uploaded)
app.MapPost("/api/outreach/send", async (
    [FromForm] string recruiters,
    [FromForm] string? roleKey,
    [FromForm] string? templateKind,
    [FromForm] string? company,
    [FromForm] string? subject,
    [FromForm] string? emailBody,
    [FromForm] bool persistResume,
    IFormFile? resume,
    OutreachService outreach,
    OutreachSettings settings) =>
{
    // Allow recruiter emails to be separated by commas, semicolons, spaces, or newlines
    var recruiterEmails = Regex.Split(recruiters ?? string.Empty, "[\\s,;]+")
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToArray();

    company ??= string.Empty;

    // If a resume is uploaded, either persist it as the new default (when requested) or
    // write it to a temporary file that will be used only for this send.
    string? resumeAttachmentPath = null;
    if (resume is not null && resume.Length > 0)
    {
        if (persistResume && settings.Resume is not null && !string.IsNullOrWhiteSpace(settings.Resume.DefaultAttachmentPath))
        {
            var targetPath = settings.Resume.DefaultAttachmentPath;
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            using (var fileStream = File.Create(targetPath))
            {
                await resume.CopyToAsync(fileStream);
            }

            resumeAttachmentPath = targetPath;
        }
        else
        {
            var baseFolder = !string.IsNullOrWhiteSpace(settings.Resume?.DefaultAttachmentPath)
                ? Path.GetDirectoryName(settings.Resume.DefaultAttachmentPath) ?? Directory.GetCurrentDirectory()
                : Path.GetDirectoryName(Environment.ProcessPath!) ?? Directory.GetCurrentDirectory();

            Directory.CreateDirectory(baseFolder);

            var tempFileName = $"UploadedResume_{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{Path.GetExtension(resume.FileName)}";
            var tempPath = Path.Combine(baseFolder, tempFileName);

            using (var fileStream = File.Create(tempPath))
            {
                await resume.CopyToAsync(fileStream);
            }

            resumeAttachmentPath = tempPath;
        }
    }

    // Resolve a human-friendly role name for token replacement (e.g., "Data Analyst")
    var roleDisplayName = string.Empty;
    if (!string.IsNullOrWhiteSpace(roleKey))
    {
        var role = settings.RoleEmailTemplates
            .FirstOrDefault(r => string.Equals(r.Key, roleKey, StringComparison.OrdinalIgnoreCase));

        // Prefer DisplayName, but fall back to the key itself so {Role} is never empty
        roleDisplayName = !string.IsNullOrWhiteSpace(role?.DisplayName)
            ? role!.DisplayName
            : roleKey;
    }

    await outreach.RunAsync(
        company,
        roleDisplayName,
        jobDescription: null,
        roleKey: roleKey,
        templateKind: templateKind,
        recruiterEmails: recruiterEmails,
        subjectOverride: subject,
        emailBodyOverride: emailBody,
        resumeAttachmentPath: resumeAttachmentPath);

    return Results.Ok();
})
.DisableAntiforgery()
.WithName("SendEmails");

app.Run();

static async Task<string> ExtractResumeTextFromFileAsync(string path, CancellationToken cancellationToken)
{
    var extension = Path.GetExtension(path).ToLowerInvariant();

    if (extension == ".pdf")
    {
        await using var stream = File.OpenRead(path);
        return ExtractTextFromPdf(stream);
    }

    return await File.ReadAllTextAsync(path, cancellationToken);
}

static async Task<string> ExtractResumeTextFromUploadAsync(IFormFile file, CancellationToken cancellationToken)
{
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    if (extension == ".pdf")
    {
        await using var stream = file.OpenReadStream();
        return ExtractTextFromPdf(stream);
    }

    using var reader = new StreamReader(file.OpenReadStream());
    return await reader.ReadToEndAsync();
}

static string ExtractTextFromPdf(Stream stream)
{
    using var document = PdfDocument.Open(stream);
    var builder = new System.Text.StringBuilder();

    foreach (var page in document.GetPages())
    {
        builder.AppendLine(page.Text);
    }

    return builder.ToString();
}
