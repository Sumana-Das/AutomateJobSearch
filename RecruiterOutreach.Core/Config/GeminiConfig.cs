using System.Collections.Generic;

namespace RecruiterOutreach.Core.Config;

public sealed class GeminiModelConfig
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int MaxRequestsPerMinute { get; set; }
    public int MaxRequestsPerDay { get; set; }
}

public sealed class GeminiConfig
{
    public string DefaultModelId { get; set; } = string.Empty;
    public List<GeminiModelConfig> Models { get; set; } = new();
}
