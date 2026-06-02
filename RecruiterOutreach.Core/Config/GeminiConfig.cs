using System.Collections.Generic;

namespace RecruiterOutreach.Core.Config;

public sealed class GeminiModelConfig
{
    public string Id { get; set; } = string.Empty;          // e.g. "gemini-3.5-flash"
    public string DisplayName { get; set; } = string.Empty; // For UI dropdown
    public string Model { get; set; } = string.Empty;       // Actual Gemini model name
    public int MaxRequestsPerMinute { get; set; }
    public int MaxRequestsPerDay { get; set; }
}

public sealed class GeminiConfig
{
    public string DefaultModelId { get; set; } = string.Empty;
    public List<GeminiModelConfig> Models { get; set; } = new();
}
