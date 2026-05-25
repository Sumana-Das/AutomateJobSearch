using System.Threading;
using System.Threading.Tasks;

namespace RecruiterOutreachConsole.Gemini;

public interface IGeminiPersonalizationService
{
    Task<GeminiPersonalizationResult> PersonalizeAsync(
        string resumeText,
        string jobDescription,
        CancellationToken cancellationToken = default);
}
