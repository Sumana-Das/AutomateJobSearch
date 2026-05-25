using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RecruiterOutreach.Core.Gemini;

/// <summary>
/// Placeholder implementation that simulates Gemini personalization.
/// It is structured so you can later plug in real HTTP calls to the Gemini API using HttpClient.
/// </summary>
public sealed class GeminiPersonalizationService : IGeminiPersonalizationService
{
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiPersonalizationService> _logger;

    public GeminiPersonalizationService(GeminiSettings settings, ILogger<GeminiPersonalizationService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public Task<GeminiPersonalizationResult> PersonalizeAsync(
        string resumeText,
        string jobDescription,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Gemini] Starting placeholder personalization against JD of length {Length} characters.", jobDescription.Length);

        // Very naive keyword extraction: pick top distinct capitalized words.
        var keywords = ExtractKeywords(jobDescription).Take(10).ToList();

        var suggestionsBuilder = new System.Text.StringBuilder();
        suggestionsBuilder.AppendLine("Suggested keywords to emphasize in your resume:");
        foreach (var kw in keywords)
        {
            suggestionsBuilder.AppendLine("- " + kw);
        }

        suggestionsBuilder.AppendLine();
        suggestionsBuilder.AppendLine("Context from JD that inspired these suggestions (first part of JD):");

        var jdExcerpt = jobDescription.Length > 1000
            ? jobDescription.Substring(0, 1000) + "..."
            : jobDescription;

        suggestionsBuilder.AppendLine(jdExcerpt);

        _logger.LogInformation("[Gemini] Placeholder resume suggestions completed. Keywords used: {Keywords}", string.Join(", ", keywords));

        var result = new GeminiPersonalizationResult
        {
            UpdatedResumeText = suggestionsBuilder.ToString(),
            KeywordsToAdd = keywords,
            KeywordsToRemove = new List<string>()
        };

        return Task.FromResult(result);
    }

    private static IEnumerable<string> ExtractKeywords(string text)
    {
        var matches = Regex.Matches(text, "[A-Z][a-zA-Z0-9_]+");
        return matches
            .Select(m => m.Value)
            .Where(v => v.Length > 2)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
