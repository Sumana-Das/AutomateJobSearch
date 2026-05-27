using System.Collections.Generic;

namespace RecruiterOutreach.Core.Gemini;

public sealed class GeminiPersonalizationResult
{
    public string UpdatedResumeText { get; init; } = string.Empty;
    // Our heuristic view (computed from Gemini jd/resume keywords)
    public IReadOnlyList<string> OurKeywordsToAdd { get; init; } = new List<string>();
    public IReadOnlyList<string> OurMissingKeywords { get; init; } = new List<string>();
    public string OurMatchScore { get; init; } = "Unknown";

    // Gemini's own view (as provided by the model)
    public IReadOnlyList<string> GeminiKeywordsToAdd { get; init; } = new List<string>();
    public IReadOnlyList<string> GeminiMissingKeywords { get; init; } = new List<string>();
    public string GeminiMatchScore { get; init; } = "Unknown";
}
