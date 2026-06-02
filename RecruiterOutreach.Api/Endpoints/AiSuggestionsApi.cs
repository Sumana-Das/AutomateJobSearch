using Microsoft.AspNetCore.Mvc;
using RecruiterOutreach.Api.Contracts;
using RecruiterOutreach.Core;
using RecruiterOutreach.Core.Gemini;
using RecruiterOutreach.Core.Utilities;

namespace RecruiterOutreach.Api.Endpoints;

/// <summary>
/// Maps AI suggestions endpoint used by the suggestions tab to call Gemini and compute scoring.
/// </summary>
public static class AiSuggestionsApi
{
    public static IEndpointRouteBuilder MapAiSuggestionsApi(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/outreach/suggestions", async (
            [FromForm] string jobDescription,
            [FromForm] bool persistResume,
            [FromForm] string? modelId,
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
                resumeText = await ResumeTextExtractor.ExtractFromStreamAsync(
                    resume.OpenReadStream(),
                    Path.GetExtension(resume.FileName),
                    cancellationToken);

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
                    resumeText = await ResumeTextExtractor.ExtractFromFileAsync(defaultPath, cancellationToken);
                }
            }

            var personalization = await gemini.ResumeSuggestionAsync(resumeText, jobDescription, modelId, cancellationToken);

            var jdExcerpt = jobDescription.Length > 1000
                ? jobDescription.Substring(0, 1000) + "..."
                : jobDescription;

            // For debugging multi-page resume parsing, expose the full parsed text back to the UI.
            // This is not used for any scoring, only for inspection in the browser console.
            var resumePreview = resumeText ?? string.Empty;

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
                jdExcerpt,
                resumePreview);

            return Results.Ok(response);
        })
        .DisableAntiforgery()
        .WithName("GetSuggestions");

        return app;
    }
}
