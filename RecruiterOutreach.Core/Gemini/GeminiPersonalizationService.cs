using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Google.GenAI;
using RecruiterOutreach.Core.Config;

namespace RecruiterOutreach.Core.Gemini;

/// <summary>
/// Orchestrates Gemini calls to generate resume personalization narrative and keyword-based match scoring with built-in rate limiting.
/// </summary>
public sealed class GeminiPersonalizationService : IGeminiPersonalizationService
{
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiPersonalizationService> _logger;
    private readonly Client _client;
    private readonly int _rpmLimit;
    private readonly int _rpdLimit;
    private readonly object _rateLock = new();
    private readonly Queue<DateTime> _minuteWindow = new();
    private readonly Queue<DateTime> _dayWindow = new();
    private readonly Dictionary<string, Queue<DateTime>> _minuteWindowsByModel = new();
    private readonly Dictionary<string, Queue<DateTime>> _dayWindowsByModel = new();

    private readonly string _resumeSuggestionPromptTemplate;
    private readonly double _highThreshold;
    private readonly double _mediumThreshold;
    private readonly GeminiConfig? _geminiConfig;

    public GeminiPersonalizationService(
        GeminiSettings settings,
        ScoringSettings scoring,
        ILogger<GeminiPersonalizationService> logger,
        Client client,
        GeminiConfig? geminiConfig = null)
    {
        _settings = settings;
        _logger = logger;
        _client = client;
        _geminiConfig = geminiConfig;

        _highThreshold = scoring?.HighThreshold ?? 0.7;
        _mediumThreshold = scoring?.MediumThreshold ?? 0.4;

        // Rate limits must be supplied from configuration; if omitted, throttling is disabled.
        _rpmLimit = _settings.MaxRequestsPerMinute ?? 0;
        _rpdLimit = _settings.MaxRequestsPerDay ?? 0;

        if (string.IsNullOrWhiteSpace(_settings.ResumeSuggestionPromptTemplate))
        {
            throw new InvalidOperationException("Gemini ResumeSuggestionPromptTemplate must be configured in OutreachSettings.Gemini.");
        }

        _resumeSuggestionPromptTemplate = _settings.ResumeSuggestionPromptTemplate;
    }

    public async Task<GeminiPersonalizationResult> ResumeSuggestionAsync(
        string resumeText,
        string jobDescription,
        string? modelId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Gemini] Starting personalization against JD of length {Length} characters.", jobDescription.Length);

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException("Gemini settings are not configured. Please set Model and ApiKey.");
        }

        var prompt = BuildResumeSuggestionPrompt(jobDescription, resumeText);

        // Resolve effective model configuration based on requested modelId and GeminiConfig (if present).
        string effectiveModel = _settings.Model;
        int rpmLimit = _rpmLimit;
        int rpdLimit = _rpdLimit;
        string limiterKey = "default";

        GeminiModelConfig? selectedModelConfig = null;
        if (_geminiConfig is not null && _geminiConfig.Models is { Count: > 0 })
        {
            var requestedId = string.IsNullOrWhiteSpace(modelId)
                ? _geminiConfig.DefaultModelId
                : modelId!;

            selectedModelConfig = _geminiConfig.Models
                .FirstOrDefault(m => string.Equals(m.Id, requestedId, StringComparison.OrdinalIgnoreCase))
                ?? _geminiConfig.Models.First();

            effectiveModel = string.IsNullOrWhiteSpace(selectedModelConfig.Model)
                ? effectiveModel
                : selectedModelConfig.Model;

            rpmLimit = selectedModelConfig.MaxRequestsPerMinute;
            rpdLimit = selectedModelConfig.MaxRequestsPerDay;
            limiterKey = selectedModelConfig.Id ?? effectiveModel ?? "default";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(effectiveModel))
            {
                throw new InvalidOperationException("Gemini model must be configured either in OutreachSettings.Gemini or GeminiConfig.");
            }

            limiterKey = effectiveModel;
        }

        try
        {
            // Find or create rate-limit windows for this model key
            Queue<DateTime> minuteWindow;
            Queue<DateTime> dayWindow;
            lock (_rateLock)
            {
                if (!_minuteWindowsByModel.TryGetValue(limiterKey, out minuteWindow!))
                {
                    minuteWindow = new Queue<DateTime>();
                    _minuteWindowsByModel[limiterKey] = minuteWindow;
                }

                if (!_dayWindowsByModel.TryGetValue(limiterKey, out dayWindow!))
                {
                    dayWindow = new Queue<DateTime>();
                    _dayWindowsByModel[limiterKey] = dayWindow;
                }
            }

            await GeminiRateLimiter.ThrottleAsync(
                rpmLimit,
                rpdLimit,
                _rateLock,
                minuteWindow,
                dayWindow,
                cancellationToken);

            var response = await _client.Models.GenerateContentAsync(
                model: effectiveModel,
                contents: prompt);

            var text = response.Text;

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Gemini response did not contain any text.");
            }

            var (narrative, jdKeywords, resumeKeywords, missingKeywords, keywordsToAdd, geminiMatchScore) =
                ParseGeminiResponse(text);

            var ourMatchScore = ComputeOurMatchScore(jdKeywords, resumeKeywords, ref missingKeywords);

            return BuildResult(
                narrative,
                jdKeywords,
                missingKeywords,
                ourMatchScore,
                keywordsToAdd,
                geminiMatchScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Gemini] Failed to parse response JSON.");
            throw new InvalidOperationException("Failed to parse Gemini response.", ex);
        }
    }
    private string BuildResumeSuggestionPrompt(string jobDescription, string resumeText)
    {
        var prompt = _resumeSuggestionPromptTemplate;

        prompt = prompt.Replace("{{JobDescription}}", jobDescription ?? string.Empty, StringComparison.Ordinal);
        prompt = prompt.Replace("{{ResumeText}}", resumeText ?? string.Empty, StringComparison.Ordinal);

        return prompt;
    }

    private static (string Narrative,
        List<string> JdKeywords,
        List<string> ResumeKeywords,
        List<string> MissingKeywords,
        List<string> KeywordsToAdd,
        string GeminiMatchScore) ParseGeminiResponse(string text)
    {
        const string marker = "JSON_KEYWORDS_START";
        var markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            throw new InvalidOperationException("Gemini response did not contain the expected JSON marker.");
        }

        var narrativePart = text.Substring(0, markerIndex).TrimEnd();
        var jsonPart = text.Substring(markerIndex + marker.Length).Trim();

        if (string.IsNullOrWhiteSpace(jsonPart))
        {
            throw new InvalidOperationException("Gemini response did not contain the expected JSON block after the marker.");
        }

        using var resultDoc = JsonDocument.Parse(jsonPart);
        var resultRoot = resultDoc.RootElement;

        var geminiMatchScore = resultRoot.GetProperty("matchScore").GetString() ?? "Unknown";

        var jdKeywordsFromGemini = ExtractKeywordsArray(resultRoot, "jdKeywords");
        var resumeKeywordsFromGemini = ExtractKeywordsArray(resultRoot, "resumeKeywords");
        var missingKeywordsFromGemini = ExtractKeywordsArray(resultRoot, "missingKeywords");
        var keywordsToAddFromGemini = ExtractKeywordsArray(resultRoot, "keywordsToAdd");

        return (narrativePart,
            jdKeywordsFromGemini,
            resumeKeywordsFromGemini,
            missingKeywordsFromGemini,
            keywordsToAddFromGemini,
            geminiMatchScore);
    }

    private static List<string> ExtractKeywordsArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array)
        {
            return new List<string>();
        }

        return prop
            .EnumerateArray()
            .Select(e => e.GetString() ?? string.Empty)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string ComputeOurMatchScore(
        List<string> jdKeywordsFromGemini,
        List<string> resumeKeywordsFromGemini,
        ref List<string> missingKeywordsFromGemini)
    {
        if (jdKeywordsFromGemini == null || jdKeywordsFromGemini.Count == 0)
        {
            return "Unknown";
        }

        var jdDistinct = jdKeywordsFromGemini
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var resumeDistinct = (resumeKeywordsFromGemini ?? new List<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var ourMissing = jdDistinct
            .Where(k => !resumeDistinct.Contains(k, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var presentCount = jdDistinct.Count - ourMissing.Count;
        var ratio = presentCount / (double)jdDistinct.Count;

        var ourMatchScore = ratio >= _highThreshold
            ? "High"
            : ratio >= _mediumThreshold
                ? "Medium"
                : "Low";

        // Prefer ourMissing over the model's missingKeywords for deterministic behavior
        missingKeywordsFromGemini = ourMissing;

        return ourMatchScore;
    }

    private static GeminiPersonalizationResult BuildResult(
        string suggestionsText,
        List<string> jdKeywordsFromGemini,
        List<string> missingKeywordsFromGemini,
        string ourMatchScore,
        List<string> keywordsToAddFromGemini,
        string geminiMatchScore)
    {
        return new GeminiPersonalizationResult
        {
            UpdatedResumeText = suggestionsText,

            OurKeywordsToAdd = jdKeywordsFromGemini ?? new List<string>(),
            OurMissingKeywords = missingKeywordsFromGemini ?? new List<string>(),
            OurMatchScore = ourMatchScore,

            GeminiKeywordsToAdd = keywordsToAddFromGemini ?? new List<string>(),
            GeminiMissingKeywords = missingKeywordsFromGemini ?? new List<string>(),
            GeminiMatchScore = geminiMatchScore
        };
    }
}
