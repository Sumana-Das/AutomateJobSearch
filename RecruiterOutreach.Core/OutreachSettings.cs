using System.Collections.Generic;

namespace RecruiterOutreach.Core;

public sealed class OutreachSettings
{
    public List<string> Recruiters { get; set; } = new();
    public List<RoleEmailTemplateSettings> RoleEmailTemplates { get; set; } = new();
    public string? DefaultRoleKey { get; set; }
    public string? DefaultTemplateKind { get; set; }
    public ResumeSettings Resume { get; set; } = new();
    public SmtpSettings SmtpSettings { get; set; } = new();
    public GeminiSettings Gemini { get; set; } = new();
    // Keywords in recruiter email local-parts that should be treated as generic inboxes
    public List<string> GenericRecruiterKeywords { get; set; } = new();
    public ScoringSettings Scoring { get; set; } = new();
}

public sealed class RoleEmailTemplateSettings
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public RoleTemplateVariants Templates { get; set; } = new();
}

public sealed class RoleTemplateVariants
{
    public RoleTemplateVariant? Hr { get; set; }
    public RoleTemplateVariant? Referral { get; set; }
}

public sealed class RoleTemplateVariant
{
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
}

public sealed class ResumeSettings
{
    public string DefaultAttachmentPath { get; set; } = string.Empty;
}

public sealed class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public bool UseSsl { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
}

public sealed class GeminiSettings
{
    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int? MaxRequestsPerMinute { get; set; }
    public int? MaxRequestsPerDay { get; set; }
    public string ResumeSuggestionPromptTemplate { get; set; } = string.Empty;
}

public sealed class ScoringSettings
{
    public double HighThreshold { get; set; } = 0.7;
    public double MediumThreshold { get; set; } = 0.4;
}
