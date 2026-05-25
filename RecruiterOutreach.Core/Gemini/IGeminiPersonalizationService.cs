using System.Threading;
using System.Threading.Tasks;

namespace RecruiterOutreach.Core.Gemini;

public interface IGeminiPersonalizationService
{
    Task<GeminiPersonalizationResult> PersonalizeAsync(
        string resumeText,
        string jobDescription,
        CancellationToken cancellationToken = default);
}
