namespace RecruiterOutreach.Api.Contracts;

public record SuggestionsRequest(string TemplateKey, string Company, string Role, string JobDescription);

public record SuggestionsResponse(string SuggestionsText, IReadOnlyList<string> KeywordsToAdd, string JdExcerpt);
