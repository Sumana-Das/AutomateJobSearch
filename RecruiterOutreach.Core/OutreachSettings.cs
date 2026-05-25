using System.Collections.Generic;

namespace RecruiterOutreach.Core;

public sealed class OutreachSettings
{
    public List<string> Recruiters { get; set; } = new();
    public EmailTemplateSettings EmailTemplate { get; set; } = new();
    public ResumeSettings Resume { get; set; } = new();
    public SmtpSettings SmtpSettings { get; set; } = new();
    public GeminiSettings Gemini { get; set; } = new();
}

public sealed class EmailTemplateSettings
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
    public string Endpoint { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
