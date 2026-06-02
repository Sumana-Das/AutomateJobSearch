using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using RecruiterOutreach.Core;
using RecruiterOutreach.Core.Outreach;

namespace RecruiterOutreach.Api.Endpoints;

/// <summary>
/// Maps email-sending and template-related endpoints used by the email sender tab.
/// </summary>
public static class EmailSenderApi
{
    public static IEndpointRouteBuilder MapEmailSenderApi(this IEndpointRouteBuilder app)
    {
        // /api/templates
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

        // /api/templates/preview
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

        // /api/outreach/send
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

        return app;
    }
}
