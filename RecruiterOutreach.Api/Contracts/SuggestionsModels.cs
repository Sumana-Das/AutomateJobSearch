namespace RecruiterOutreach.Api.Contracts;

public record SuggestionsRequest(string JobDescription, bool PersistResume);

public record ScoringView(
    IReadOnlyList<string> KeywordsToAdd,
    IReadOnlyList<string> MissingKeywords,
    string MatchScore);

public record SuggestionsResponse(
    string SuggestionsText,
    ScoringView OurScoring,
    ScoringView GeminiScoring,
    string JdExcerpt);

public record SetupRequest(string? GmailAppPassword, string? GeminiApiKey);

public record StatusResponse(bool GmailConfigured, bool GeminiConfigured, string? SmtpUserName);
