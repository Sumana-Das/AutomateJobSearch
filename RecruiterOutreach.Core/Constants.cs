namespace RecruiterOutreach.Core
{
    public static class Constants
    {
        public static class TemplateKinds
        {
            public const string Hr = "Hr";
            public const string Referral = "Referral";
        }

        public static class Placeholders
        {
            public const string Company = "{Company}";
            public const string Role = "{Role}";
            public const string RecruiterName = "{RecruiterName}";
        }

        public static class Gemini
        {
            public static class Errors
            {
                public const string MissingResumePrompt = "Gemini ResumeSuggestionPromptTemplate must be configured in OutreachSettings.Gemini.";
                public const string MissingConfig = "Gemini settings are not configured. Please set Model and ApiKey.";
                public const string ParseFailed = "Failed to parse Gemini response.";
                public const string MissingModel = "Gemini model must be configured either in OutreachSettings.Gemini or GeminiConfig.";
                public const string ResponseNoText = "Gemini response did not contain any text.";
                public const string ResponseNoMarker = "Gemini response did not contain the expected JSON marker.";
                public const string ResponseNoJsonAfterMarker = "Gemini response did not contain the expected JSON block after the marker.";
            }

            public static class Logs
            {
                public const string ParseFailedTag = "[Gemini] Failed to parse response JSON.";
                public const string StartingPersonalization = "[Gemini] Starting personalization against JD of length {Length} characters.";
            }

            public static class Prompt
            {
                public const string PlaceholderJobDescription = "{{JobDescription}}";
                public const string PlaceholderResumeText = "{{ResumeText}}";
            }

            public static class Response
            {
                public const string JsonMarker = "JSON_KEYWORDS_START";
                public const string PropMatchScore = "matchScore";
                public const string PropJdKeywords = "jdKeywords";
                public const string PropResumeKeywords = "resumeKeywords";
                public const string PropMissingKeywords = "missingKeywords";
                public const string PropKeywordsToAdd = "keywordsToAdd";
                public const string Unknown = "Unknown";
            }

            public static class MatchScore
            {
                public const string High = "High";
                public const string Medium = "Medium";
                public const string Low = "Low";
            }

            public static class Limiter
            {
                public const string DefaultKey = "default";
            }
        }

        public static class Logs
        {
            public static class Outreach
            {
                public const string StartingRun = "Starting outreach run for Company={Company}, Role={Role}";
                public const string JDDraftMode = "JD provided. Running in DRAFT mode (no emails will be sent).";
                public const string SuggestionsCreatedAt = "Resume suggestions file created at {Path}. Review and manually update your DOCX/PDF.";
                public const string NoRecruiters = "No recruiter email addresses provided. Aborting outreach run.";
                public const string DefaultResumeMissing = "Configured default resume attachment does not exist at {Path}";
                public const string SendingTo = "Sending outreach email to {Recruiter}";
                public const string SendFailed = "Failed to send email to {Recruiter}";
                public const string Completed = "Outreach run completed.";
            }
        }

        public static class Labels
        {
            public const string Company = "Company: ";
            public const string Role = "Role: ";
            public const string OurScoringHeader = "Our scoring (heuristic overlap using Gemini JD/resume keywords):";
            public const string KeywordsToAdd = "Keywords to consider adding:";
            public const string MissingKeywords = "Keywords currently missing from your resume:";
            public const string GeminiScoringHeader = "Gemini scoring (direct from model):";
            public const string KeywordsToAddGemini = "Keywords to consider adding (Gemini):";
            public const string MissingKeywordsGemini = "Keywords currently missing (Gemini view):";
            public const string DetailedSuggestions = "Detailed suggestions:";
            public const string BulletPrefix = "- ";
            public const string MatchScore = "Match score: ";
        }

        public static class Files
        {
            public const string SuggestionsDir = "Suggestions";
            public const string SuggestionsFilePrefix = "ResumeSuggestions_";
            public const string SuggestionsFileTimestampFormat = "yyyyMMddHHmmss";
            public const string SuggestionsFileExtension = ".txt";
        }

        public static class Defaults
        {
            public const string RecruiterName = "Recruiter";
        }

        public static class Email
        {
            public const bool IsBodyHtml = false;

            public static class Logs
            {
                public const string AttachmentNotFound = "Attachment file not found at path {AttachmentPath}";
                public const string Sending = "Sending email to {To} with subject '{Subject}'";
                public const string Sent = "Email successfully sent to {To}";
            }
        }

        public static class Smtp
        {
            public static class Env
            {
                public const string GmailAppPasswordKey = "GMAIL_APP_PASSWORD";
            }
            public static class Errors
            {
                public const string AppPasswordMissingLog = "GMAIL_APP_PASSWORD environment variable is not set. Cannot authenticate to SMTP server.";
                public const string AppPasswordMissing = "GMAIL_APP_PASSWORD environment variable is not set.";
            }
        }

        public static class FileExtensions
        {
            public const string Pdf = ".pdf";
        }

        public static class RateLimiter
        {
            public const int MinuteWindowSeconds = 60;
            public const int DayWindowDays = 1;
            public const string DailyLimitReached = "Gemini daily rate limit reached for this application. Please try again tomorrow or upgrade your plan.";
        }

        public static class Scoring
        {
            public const double DefaultHighThreshold = 0.7;
            public const double DefaultMediumThreshold = 0.4;
        }
    }
}
