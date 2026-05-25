using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Antiforgery;
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

// Suggestions endpoint (JD-based, no send)
app.MapPost("/api/outreach/suggestions", async (
    [FromBody] SuggestionsRequest request,
    IGeminiPersonalizationService gemini) =>
{
    var personalization = await gemini.PersonalizeAsync(string.Empty, request.JobDescription);

    var jdExcerpt = request.JobDescription.Length > 1000
        ? request.JobDescription.Substring(0, 1000) + "..."
        : request.JobDescription;

    var response = new SuggestionsResponse(
        personalization.UpdatedResumeText,
        personalization.KeywordsToAdd,
        jdExcerpt);

    return Results.Ok(response);
})
.WithName("GetSuggestions");

// Basic send endpoint (no JD-based personalization; uses default attachment)
app.MapPost("/api/outreach/send", async (
    [FromForm] string recruiters,
    [FromForm] string? roleKey,
    [FromForm] string? templateKind,
    [FromForm] string? company,
    [FromForm] string? subject,
    [FromForm] string? emailBody,
    OutreachService outreach,
    OutreachSettings settings) =>
{
    var recruiterEmails = (recruiters ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToArray();

    company ??= string.Empty;

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
        emailBodyOverride: emailBody);

    return Results.Ok();
})
.DisableAntiforgery()
.WithName("SendEmails");

app.Run();
