using Microsoft.AspNetCore.Mvc;
using System.Web;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using RecruiterOutreach.Api.Services;

namespace RecruiterOutreach.Api.Endpoints;

public static class IdentityApi
{
    public static IEndpointRouteBuilder MapIdentityApi(this IEndpointRouteBuilder app)
    {
        // Initiate Google OAuth flow
        app.MapGet("/auth/google", ([FromQuery] string? redirect, HttpContext http) =>
        {
            var clientId = Environment.GetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_ID");
            var redirectUri = Environment.GetEnvironmentVariable("GOOGLE_OAUTH_REDIRECT_URI");
            var clientAppBaseUrl = Environment.GetEnvironmentVariable("CLIENT_APP_BASE_URL");
            if (string.IsNullOrWhiteSpace(clientAppBaseUrl))
            {
                // Default SPA dev server
                clientAppBaseUrl = "http://localhost:3000";
            }

            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                // Sensible local default; adjust if your backend runs on a different port
                var request = http.Request;
                var scheme = request.Scheme;
                var host = request.Host.HasValue ? request.Host.Value : "localhost:5001";
                redirectUri = $"{scheme}://{host}/auth/google/callback";
            }

            if (string.IsNullOrWhiteSpace(clientId))
            {
                return Results.BadRequest(new { error = "Missing GOOGLE_OAUTH_CLIENT_ID. Configure it to enable Google Sign-In." });
            }

            var scopes = new[]
            {
                "openid",
                "https://www.googleapis.com/auth/userinfo.email",
                "https://www.googleapis.com/auth/userinfo.profile",
                "https://www.googleapis.com/auth/gmail.send"
            };

            var query = HttpUtility.ParseQueryString(string.Empty);
            query["response_type"] = "code";
            query["client_id"] = clientId;
            query["redirect_uri"] = redirectUri;
            query["scope"] = string.Join(' ', scopes);
            query["access_type"] = "offline"; // request refresh_token on first consent
            query["prompt"] = "consent";      // force consent to ensure refresh_token
            // Always include a state so callback can return to SPA; prefer provided redirect, fallback to SPA base URL
            query["state"] = string.IsNullOrWhiteSpace(redirect) ? clientAppBaseUrl : redirect;

            var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth?{query.ToString()}";
            return Results.Redirect(authUrl);
        })
        .WithName("StartGoogleOAuth");

        // Google OAuth callback: exchange code for tokens, fetch profile, set secure cookie, and redirect to SPA
        app.MapGet("/auth/google/callback", async ([FromQuery] string? code, [FromQuery] string? state, HttpContext http, InMemoryTokenStore tokens) =>
        {
            var clientAppBaseUrl = Environment.GetEnvironmentVariable("CLIENT_APP_BASE_URL");
            if (string.IsNullOrWhiteSpace(clientAppBaseUrl)) clientAppBaseUrl = "http://localhost:3000";

            if (string.IsNullOrWhiteSpace(code))
            {
                return Results.BadRequest(new { error = "Missing authorization code." });
            }

            var clientId = Environment.GetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("GOOGLE_OAUTH_CLIENT_SECRET");
            var redirectUri = Environment.GetEnvironmentVariable("GOOGLE_OAUTH_REDIRECT_URI");
            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                var request = http.Request;
                var scheme = request.Scheme;
                var host = request.Host.HasValue ? request.Host.Value : "localhost:5001";
                redirectUri = $"{scheme}://{host}/auth/google/callback";
            }

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                return Results.Problem("Google OAuth client credentials are not configured.", statusCode: 500);
            }

            try
            {
                // 1) Exchange code for tokens
                using var httpClient = new HttpClient();
                var tokenReq = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
                tokenReq.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["redirect_uri"] = redirectUri,
                    ["grant_type"] = "authorization_code",
                });
                var tokenRes = await httpClient.SendAsync(tokenReq);
                if (!tokenRes.IsSuccessStatusCode)
                {
                    var err = await tokenRes.Content.ReadAsStringAsync();
                    return Results.Problem($"Token exchange failed: {err}", statusCode: 502);
                }
                using var tokenDoc = JsonDocument.Parse(await tokenRes.Content.ReadAsStringAsync());
                var root = tokenDoc.RootElement;
                var accessToken = root.GetProperty("access_token").GetString();
                var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

                // 2) Fetch user profile
                var profileReq = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
                profileReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var profileRes = await httpClient.SendAsync(profileReq);
                if (!profileRes.IsSuccessStatusCode)
                {
                    var err = await profileRes.Content.ReadAsStringAsync();
                    return Results.Problem($"Profile fetch failed: {err}", statusCode: 502);
                }

                var profileJson = await profileRes.Content.ReadAsStringAsync();
                using var profileDoc = JsonDocument.Parse(profileJson);
                var name = profileDoc.RootElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
                var email = profileDoc.RootElement.TryGetProperty("email", out var emailProp) ? emailProp.GetString() : null;

                // 3) Store tokens server-side (in-memory for dev)
                if (!string.IsNullOrWhiteSpace(email))
                {
                    tokens.Upsert(email, accessToken ?? string.Empty, refreshToken, DateTimeOffset.UtcNow.AddMinutes(50));
                }

                // 4) Set a secure, HttpOnly cookie with minimal user info (no tokens in cookie)
                var userInfo = new { name = name ?? string.Empty, email = email ?? string.Empty };
                var cookiePayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(userInfo)));
                http.Response.Cookies.Append(
                    "tm_user",
                    cookiePayload,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddDays(7)
                    }
                );

                // 5) Redirect back to SPA
                var target = string.IsNullOrWhiteSpace(state) ? clientAppBaseUrl : state;
                var separator = target.Contains('?') ? '&' : '?';
                return Results.Redirect($"{target}{separator}login=success");
            }
            catch (Exception ex)
            {
                return Results.Problem($"OAuth callback error: {ex.Message}", statusCode: 500);
            }
        })
        .WithName("GoogleOAuthCallback");

        // Lightweight identity endpoint for SPA to read name/email set by the callback
        app.MapGet("/api/me", (HttpContext http) =>
        {
            if (!http.Request.Cookies.TryGetValue("tm_user", out var val) || string.IsNullOrWhiteSpace(val))
            {
                return Results.Unauthorized();
            }
            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(val));
                var doc = JsonDocument.Parse(json);
                var name = doc.RootElement.TryGetProperty("name", out var n) ? n.GetString() : string.Empty;
                var email = doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() : string.Empty;
                return Results.Ok(new { name, email });
            }
            catch
            {
                return Results.Unauthorized();
            }
        })
        .WithName("GetCurrentUser");

        return app;
    }
}
