using System.Threading;
using System.Threading.Tasks;

namespace RecruiterOutreach.Core.Gemini;

public interface IGeminiPersonalizationService
{
    Task<GeminiPersonalizationResult> ResumeSuggestionAsync(string resumeText, string jobDescription, string? modelId, CancellationToken cancellationToken = default);
}
