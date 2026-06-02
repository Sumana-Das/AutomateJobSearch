using System.Collections.Concurrent;

namespace RecruiterOutreach.Api.Services;

public sealed class InMemoryTokenStore
{
    private readonly ConcurrentDictionary<string, (string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt)> _tokens = new();

    public void Upsert(string email, string accessToken, string? refreshToken, DateTimeOffset expiresAt)
    {
        _tokens[email] = (accessToken, refreshToken, expiresAt);
    }

    public bool TryGet(string email, out string accessToken, out string? refreshToken)
    {
        accessToken = string.Empty;
        refreshToken = null;
        if (!_tokens.TryGetValue(email, out var t)) return false;
        accessToken = t.AccessToken;
        refreshToken = t.RefreshToken;
        return true;
    }
}
