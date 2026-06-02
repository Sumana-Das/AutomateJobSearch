namespace RecruiterOutreach.Api.Contracts;

public record SuggestionsRequest(string JobDescription, bool PersistResume);

public record SuggestionsResponse(string SuggestionsText, ScoringView OurScoring, ScoringView GeminiScoring, string JdExcerpt, string DebugResumePreview);

public record ScoringView(IReadOnlyList<string> KeywordsToAdd, IReadOnlyList<string> MissingKeywords, string MatchScore);
