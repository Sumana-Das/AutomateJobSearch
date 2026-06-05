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
        app.MapGet(Constants.ExternalApiEndpoints.GoogleAuthApi, ([FromQuery] string? redirect, HttpContext http) =>
        {
            var clientId = Environment.GetEnvironmentVariable(Constants.ConfigurationKeys.GoogleOAuthClientId);
            var redirectUri = Environment.GetEnvironmentVariable(Constants.ConfigurationKeys.GoogleOAuthRedirectUri);
            var clientAppBaseUrl = Environment.GetEnvironmentVariable(Constants.ConfigurationKeys.ClientAppBaseUrl);
            if (string.IsNullOrWhiteSpace(clientAppBaseUrl))
            {
                // Default SPA dev server
                clientAppBaseUrl = Constants.Defaults.SpaBaseUrl;
            }

            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                // Sensible local default; adjust if your backend runs on a different port
                var request = http.Request;
                var scheme = request.Scheme;
                var host = request.Host.HasValue ? request.Host.Value : Constants.Defaults.localhost;
                redirectUri = $"{scheme}://{host}{Constants.ExternalApiEndpoints.GoogleAuthCallback}";
            }

            if (string.IsNullOrWhiteSpace(clientId))
            {
                return Results.BadRequest(new { error = Constants.Responses.MissingGoogleAuthId });
            }

            var scopes = new[]
            {
                Constants.Google.OAuthParams.OpenId,
                Constants.ExternalApiEndpoints.UserInfoEmail,
                Constants.ExternalApiEndpoints.UserInfoProfile,
                Constants.ExternalApiEndpoints.GmailSend
            };

            var query = HttpUtility.ParseQueryString(string.Empty);
            query[Constants.Google.OAuthParams.Query_ResponseType] = Constants.Google.OAuthParams.ResponseType;
            query[Constants.Google.OAuthParams.Query_ClientId] = clientId;
            query[Constants.Google.OAuthParams.Query_RedirectUri] = redirectUri;
            query[Constants.Google.OAuthParams.Query_Scope] = string.Join(' ', scopes);
            query[Constants.Google.OAuthParams.Query_AccessType] = Constants.Google.OAuthParams.AccessType; // request refresh_token on first consent
            query[Constants.Google.OAuthParams.Query_Prompt] = Constants.Google.OAuthParams.Prompt;      // force consent to ensure refresh_token
            // Always include a state so callback can return to SPA; prefer provided redirect, fallback to SPA base URL
            query[Constants.Google.OAuthParams.Query_State] = string.IsNullOrWhiteSpace(redirect) ? clientAppBaseUrl : redirect;

            var authUrl = $"{Constants.ExternalApiEndpoints.AuthUrl}?{query.ToString()}";
            return Results.Redirect(authUrl);
        })
        .WithName(Constants.Defaults.StartGoogleOAuth);

        // Google OAuth callback: exchange code for tokens, fetch profile, set secure cookie, and redirect to SPA
        app.MapGet(Constants.ExternalApiEndpoints.GoogleAuthCallback, async ([FromQuery] string? code, [FromQuery] string? state, HttpContext http, InMemoryTokenStore tokens) =>
        {
            var clientAppBaseUrl = Environment.GetEnvironmentVariable(Constants.ConfigurationKeys.ClientAppBaseUrl);
            if (string.IsNullOrWhiteSpace(clientAppBaseUrl)) clientAppBaseUrl = Constants.Defaults.SpaBaseUrl;

            if (string.IsNullOrWhiteSpace(code))
            {
                return Results.BadRequest(new { error = Constants.Responses.MissingAuthorizationCode });
            }

            var clientId = Environment.GetEnvironmentVariable(Constants.ConfigurationKeys.GoogleOAuthClientId);
            var clientSecret = Environment.GetEnvironmentVariable(Constants.ConfigurationKeys.GoogleOAuthClientSecret);
            var redirectUri = Environment.GetEnvironmentVariable(Constants.ConfigurationKeys.GoogleOAuthRedirectUri);
            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                var request = http.Request;
                var scheme = request.Scheme;
                var host = request.Host.HasValue ? request.Host.Value : Constants.Defaults.localhost;
                redirectUri = $"{scheme}://{host}{Constants.ExternalApiEndpoints.GoogleAuthCallback}";
            }

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                return Results.Problem(Constants.Responses.GoogleCredentialsMissing, statusCode: 500);
            }

            try
            {
                // 1) Exchange code for tokens
                using var httpClient = new HttpClient();
                var tokenReq = new HttpRequestMessage(HttpMethod.Post, Constants.ExternalApiEndpoints.TokenUrl);
                tokenReq.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["redirect_uri"] = redirectUri,
                    ["grant_type"] = Constants.Google.OAuthParams.GrantType_AuthorizationCode,
                });
                var tokenRes = await httpClient.SendAsync(tokenReq);
                if (!tokenRes.IsSuccessStatusCode)
                {
                    var err = await tokenRes.Content.ReadAsStringAsync();
                    return Results.Problem($"{Constants.Responses.TokenExchangeFailedPrefix}{err}", statusCode: 502);
                }
                using var tokenDoc = JsonDocument.Parse(await tokenRes.Content.ReadAsStringAsync());
                var root = tokenDoc.RootElement;
                var accessToken = root.GetProperty("access_token").GetString();
                var refreshToken = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null;

                // 2) Fetch user profile
                var profileReq = new HttpRequestMessage(HttpMethod.Get, Constants.ExternalApiEndpoints.UserInfoUrl);
                profileReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(Constants.Google.OAuthParams.AuthHeader_Bearer, accessToken);
                var profileRes = await httpClient.SendAsync(profileReq);
                if (!profileRes.IsSuccessStatusCode)
                {
                    var err = await profileRes.Content.ReadAsStringAsync();
                    return Results.Problem($"{Constants.Responses.ProfileFetchFailedPrefix}{err}", statusCode: 502);
                }

                var profileJson = await profileRes.Content.ReadAsStringAsync();
                using var profileDoc = JsonDocument.Parse(profileJson);
                var name = profileDoc.RootElement.TryGetProperty(Constants.Defaults.JsonPropName, out var nameProp) ? nameProp.GetString() : null;
                var email = profileDoc.RootElement.TryGetProperty(Constants.Defaults.JsonPropEmail, out var emailProp) ? emailProp.GetString() : null;

                // 3) Store tokens server-side (in-memory for dev)
                if (!string.IsNullOrWhiteSpace(email))
                {
                    tokens.Upsert(email, accessToken ?? string.Empty, refreshToken, DateTimeOffset.UtcNow.AddMinutes(Constants.Defaults.AccessTokenSkewMinutes));
                }

                // 4) Set a secure, HttpOnly cookie with minimal user info (no tokens in cookie)
                var userInfo = new { name = name ?? string.Empty, email = email ?? string.Empty };
                var cookiePayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(userInfo)));
                http.Response.Cookies.Append(
                    Constants.Defaults.CookieUserName,
                    cookiePayload,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddDays(Constants.Defaults.CookieExpiryDays)
                    }
                );

                // 5) Redirect back to SPA
                var target = string.IsNullOrWhiteSpace(state) ? clientAppBaseUrl : state;
                var separator = target.Contains('?') ? '&' : '?';
                return Results.Redirect($"{target}{separator}{Constants.Defaults.LoginParamKey}={Constants.Defaults.LoginSuccessValue}");
            }
            catch (Exception ex)
            {
                return Results.Problem($"{Constants.Responses.OAuthCallbackErrorPrefix}{ex.Message}", statusCode: 500);
            }
        })
        .WithName(Constants.Defaults.GoogleOAuthCallback);

        // Lightweight identity endpoint for SPA to read name/email set by the callback
        app.MapGet(Constants.ApiEndpoints.MeApi, (HttpContext http) =>
        {
            if (!http.Request.Cookies.TryGetValue(Constants.Defaults.CookieUserName, out var val) || string.IsNullOrWhiteSpace(val))
            {
                return Results.Unauthorized();
            }
            try
            {
                var json = Encoding.UTF8.GetString(Convert.FromBase64String(val));
                var doc = JsonDocument.Parse(json);
                var name = doc.RootElement.TryGetProperty(Constants.Defaults.JsonPropName, out var n) ? n.GetString() : string.Empty;
                var email = doc.RootElement.TryGetProperty(Constants.Defaults.JsonPropEmail, out var e) ? e.GetString() : string.Empty;
                return Results.Ok(new { name, email });
            }
            catch
            {
                return Results.Unauthorized();
            }
        })
        .WithName(Constants.Defaults.GetCurrentUser);

        return app;
    }
}
