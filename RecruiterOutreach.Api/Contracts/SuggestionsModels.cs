namespace RecruiterOutreach.Api.Contracts;

public record SuggestionsRequest(string JobDescription);

public record SuggestionsResponse(string SuggestionsText, IReadOnlyList<string> KeywordsToAdd, string JdExcerpt);

public record SetupRequest(string? GmailAppPassword, string? GeminiApiKey);

public record StatusResponse(bool GmailConfigured, bool GeminiConfigured, string? SmtpUserName);
