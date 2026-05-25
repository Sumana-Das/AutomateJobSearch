using System.Collections.Generic;

namespace RecruiterOutreachConsole.Gemini;

public sealed class GeminiPersonalizationResult
{
    public string UpdatedResumeText { get; init; } = string.Empty;
    public IReadOnlyList<string> KeywordsToAdd { get; init; } = new List<string>();
    public IReadOnlyList<string> KeywordsToRemove { get; init; } = new List<string>();
}
