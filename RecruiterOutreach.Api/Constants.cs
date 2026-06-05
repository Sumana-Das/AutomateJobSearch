namespace RecruiterOutreach.Api
{
    public class Constants
    {
        public static class ApiEndpoints
        {
            public const string SuggestionApi = "/api/outreach/suggestions";
            public const string GetTemplatesApi = "/api/templates";
            public const string GetTemplatePreviewApi = "/api/templates/preview";
            public const string SendEmailApi = "/api/outreach/send";
            public const string MeApi = "/api/me";
        }

        public static class ExternalApiEndpoints 
        {
            public const string GoogleAuthCallback = "/auth/google/callback";
            public const string GoogleAuthApi = "/auth/google";

            public const string UserInfoEmail = "https://www.googleapis.com/auth/userinfo.email";
            public const string UserInfoProfile = "https://www.googleapis.com/auth/userinfo.profile";
            public const string GmailSend = "https://www.googleapis.com/auth/gmail.send";

            public const string AuthUrl = "https://accounts.google.com/o/oauth2/v2/auth";
            public const string TokenUrl = "https://oauth2.googleapis.com/token";
            public const string UserInfoUrl = "https://www.googleapis.com/oauth2/v2/userinfo";
        }

        public static class ConfigurationKeys
        {
            public const string GoogleOAuthClientId = "GOOGLE_OAUTH_CLIENT_ID";
            public const string GoogleOAuthClientSecret = "GOOGLE_OAUTH_CLIENT_SECRET";
            public const string GoogleOAuthRedirectUri = "GOOGLE_OAUTH_REDIRECT_URI";
            public const string ClientAppBaseUrl = "CLIENT_APP_BASE_URL";
            public const string GeminiApiKey = "GEMINI_API_KEY";
        }

        public static class Defaults
        {
            public const string localhost = "localhost:5001";
            public const string SpaBaseUrl = "http://localhost:3000";
            public const int CookieExpiryDays = 7;
            public const int AccessTokenSkewMinutes = 50; // approximate lifetime for in-memory store
            public const string ApplicationName = "TailorMailer AI";
            public const string BoundaryPrefix = "===============TAILORMAILER_";
            public const string Me = "me";
            public const int Base64WrapColumns = 76;
            public const string OutreachSettingsSection = "OutreachSettings";
            public const string ConfigDirName = "Config";
            public const string PromptsDirName = "Prompts";
            public const string ResumePromptFileName = "resume.json";
            public const string GeminiConfigFileName = "gemini.json";
            public const string EmailTemplatesDirName = "EmailTemplates";
            public const string TemplatesFilePattern = "*.json";
            public const string PromptTemplateJsonProp = "Template";
            public const string LoginParamKey = "login";
            public const string LoginSuccessValue = "success";
            public const int JobDescriptionExcerptMaxChars = 1000;
            public const string Ellipsis = "...";
            public const string WelcomeSubject = "Welcome to TailorMailer!";
            public const string AppName = "TailorMailer";
            public const string Suggestions = "GetSuggestions";
            public const string Templates = "GetTemplates";
            public const string Hr = "Hr";
            public const string Referral = "Referral";
            public const string GetTemplatePreview = "GetTemplatePreview";
            public const string SendEmails = "SendEmails";
            public const string CookieUserName = "tm_user";
            public const string JsonPropEmail = "email";
            public const string JsonPropName = "name";
            public const string StartGoogleOAuth = "StartGoogleOAuth";
            public const string GoogleOAuthCallback = "GoogleOAuthCallback";
            public const string GetCurrentUser = "GetCurrentUser";

            public static string GenerateResumeFileName(string originalFileName)
            {
                string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
                string uniqueId = Guid.NewGuid().ToString("N");
                string extension = Path.GetExtension(originalFileName);

                return $"UploadedResume_{timestamp}_{uniqueId}{extension}";
            }
        }

        // Standard Error Messages
        public static class Responses
        {
            public const string MissingGoogleAuthId = "Missing GOOGLE_OAUTH_CLIENT_ID. Configure it to enable Google Sign-In.";
            public const string MissingAuthorizationCode = "Missing authorization code.";
            public const string GoogleCredentialsMissing = "Google OAuth client credentials are not configured.";
            public const string TokenExchangeFailedPrefix = "Token exchange failed: ";
            public const string ProfileFetchFailedPrefix = "Profile fetch failed: ";
            public const string OAuthCallbackErrorPrefix = "OAuth callback error: ";
            public const string NoSignedInUser = "No signed-in user found. Please sign in with Google to send email.";
            public const string NoOAuthTokensForUser = "No OAuth tokens found for the current user. Please sign in again.";
        }

        public static class Google
        {
            public static class OAuthParams
            {
                public const string OpenId = "openid";
                public const string ResponseType = "code";
                public const string AccessType = "offline";
                public const string Prompt = "consent";
                public const string Query_ResponseType = "response_type";
                public const string Query_ClientId = "client_id";
                public const string Query_RedirectUri = "redirect_uri";
                public const string Query_Scope = "scope";
                public const string Query_AccessType = "access_type";
                public const string Query_Prompt = "prompt";
                public const string Query_State = "state";
                public const string GrantType_AuthorizationCode = "authorization_code";
                public const string AuthHeader_Bearer = "Bearer";
            }
        }

        public static class Email
        {
            public const string MimeVersionHeader = "MIME-Version: 1.0";
            public const string ContentTransferEncoding7Bit = "Content-Transfer-Encoding: 7bit";
            public const string Utf8 = "utf-8";
            public const string ApplicationPdf = "application/pdf";
            public const string TextPlain = "text/plain";
            public const string ApplicationMsWord = "application/msword";
            public const string ApplicationDocx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            public const string ImagePng = "image/png";
            public const string ImageJpeg = "image/jpeg";
            public const string ImageGif = "image/gif";
            public const string ApplicationOctetStream = "application/octet-stream";
            public const string EmailSplitPattern = "[\\s,;]+";

            public static string BuildPlainTextEmail(string from, string to, string subject, string body)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"From: {from}");
                sb.AppendLine($"To: {to}");
                sb.AppendLine($"Subject: {subject}");
                sb.AppendLine(MimeVersionHeader);
                sb.AppendLine($"Content-Type: {TextPlain}; charset=\"{Utf8}\"");
                sb.AppendLine(ContentTransferEncoding7Bit);
                sb.AppendLine();
                sb.AppendLine(body ?? string.Empty);
                return sb.ToString();
            }

            public static string BuildMultipartMixedEmail(
                string from,
                string to,
                string subject,
                string body,
                string boundary,
                string fileName,
                string mimeType,
                string fileBase64,
                int wrapColumns)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"From: {from}");
                sb.AppendLine($"To: {to}");
                sb.AppendLine($"Subject: {subject}");
                sb.AppendLine(MimeVersionHeader);
                sb.AppendLine($"Content-Type: multipart/mixed; boundary=\"{boundary}\"");
                sb.AppendLine();

                // Text part
                sb.AppendLine($"--{boundary}");
                sb.AppendLine($"Content-Type: {TextPlain}; charset=\"{Utf8}\"");
                sb.AppendLine(ContentTransferEncoding7Bit);
                sb.AppendLine();
                sb.AppendLine(body ?? string.Empty);
                sb.AppendLine();

                // Attachment part
                sb.AppendLine($"--{boundary}");
                sb.AppendLine($"Content-Type: {mimeType}; name=\"{fileName}\"");
                sb.AppendLine("Content-Transfer-Encoding: base64");
                sb.AppendLine($"Content-Disposition: attachment; filename=\"{fileName}\"");
                sb.AppendLine();

                for (int i = 0; i < fileBase64.Length; i += wrapColumns)
                {
                    var len = Math.Min(wrapColumns, fileBase64.Length - i);
                    sb.AppendLine(fileBase64.Substring(i, len));
                }

                sb.AppendLine();
                sb.AppendLine($"--{boundary}--");
                sb.AppendLine();

                return sb.ToString();
            }
        }
    }
}
