using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace RecruiterOutreach.Core.Gemini;

/// <summary>
/// Placeholder implementation that simulates Gemini personalization.
/// It is structured so you can later plug in real HTTP calls to the Gemini API using HttpClient.
/// </summary>
public sealed class GeminiPersonalizationService : IGeminiPersonalizationService
{
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiPersonalizationService> _logger;

    public GeminiPersonalizationService(GeminiSettings settings, ILogger<GeminiPersonalizationService> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    public async Task<GeminiPersonalizationResult> PersonalizeAsync(
        string resumeText,
        string jobDescription,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[Gemini] Starting personalization against JD of length {Length} characters.", jobDescription.Length);

        if (string.IsNullOrWhiteSpace(_settings.Endpoint) ||
            string.IsNullOrWhiteSpace(_settings.Model) ||
            string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            throw new InvalidOperationException("Gemini settings are not configured. Please set Endpoint, Model, and ApiKey.");
        }

        string suggestionsText;
        List<string> jdKeywordsFromGemini;
        List<string> resumeKeywordsFromGemini;
        List<string> missingKeywordsFromGemini;
        List<string> keywordsToAddFromGemini;
        string geminiMatchScore = "Unknown";

        using (var client = new HttpClient())
        {
            var endpoint = _settings.Endpoint.TrimEnd('/');
            var url = $"{endpoint}/v1beta/models/{_settings.Model}:generateContent?key={_settings.ApiKey}";

            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("You are an expert resume reviewer and ATS keyword extractor.");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("I will give you:");
            promptBuilder.AppendLine("- A job description (JD)");
            promptBuilder.AppendLine("- A candidate's resume (plain text)");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Your task:");
            promptBuilder.AppendLine("1. First, write a detailed, human-readable and well-structured explanation of how to tweak the resume for this specific JD. This should be similar to a career coach's guidance, with:");
            promptBuilder.AppendLine("   - An overall assessment paragraph.");
            promptBuilder.AppendLine("   - Clear section headings such as 'Optimize the Executive Summary', 'Restructure the Skills/Technical Expertise Section', and 'Flesh Out Professional Experience'.");
            promptBuilder.AppendLine("   - Concrete example bullet rewrites for each relevant role, written in a style suitable to paste directly into a resume.");
            promptBuilder.AppendLine("2. Then, at the very end of your response, output a small JSON block summarizing the keywords and match score.");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("CRITICAL JSON INSTRUCTIONS:");
            promptBuilder.AppendLine("- After you finish all narrative text, output a line that contains only: JSON_KEYWORDS_START");
            promptBuilder.AppendLine("- On the next line, output a single JSON object with EXACTLY this shape and no surrounding backticks or formatting:");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("{");
            promptBuilder.AppendLine("  \"matchScore\": \"High\" | \"Medium\" | \"Low\",");
            promptBuilder.AppendLine("  \"jdKeywords\": [ \"keyword1\", \"keyword2\", ... ],");
            promptBuilder.AppendLine("  \"resumeKeywords\": [ \"keyword1\", \"keyword2\", ... ],");
            promptBuilder.AppendLine("  \"missingKeywords\": [ \"keyword1\", \"keyword2\", ... ],");
            promptBuilder.AppendLine("  \"keywordsToAdd\": [ \"keyword1\", \"keyword2\", ... ]");
            promptBuilder.AppendLine("}");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Guidelines:");
            promptBuilder.AppendLine("- Focus jdKeywords and resumeKeywords on meaningful skills, technologies, responsibilities and domain terms.");
            promptBuilder.AppendLine("- Avoid generic words like \"role\", \"description\", \"the\", \"you\", \"code\", etc.");
            promptBuilder.AppendLine("- missingKeywords should be jdKeywords that are absent or clearly under-emphasized in the resume.");
            promptBuilder.AppendLine("- keywordsToAdd can be the same as missingKeywords or a curated subset you think are most critical.");
            promptBuilder.AppendLine("- matchScore should reflect how well the resume aligns with the JD overall: High, Medium, or Low.");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Now here is the input.");
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("JOB DESCRIPTION:");
            promptBuilder.AppendLine("----------------");
            promptBuilder.AppendLine(jobDescription);
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("RESUME:");
            promptBuilder.AppendLine("-------");
            promptBuilder.AppendLine(resumeText ?? string.Empty);

            var prompt = promptBuilder.ToString();

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            var response = await client.SendAsync(request, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[Gemini] API call failed with status code {StatusCode}. Body: {Body}", response.StatusCode, responseJson);
                throw new InvalidOperationException($"Gemini API call failed with status code {response.StatusCode}.");
            }

            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;
                var text = root
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (string.IsNullOrWhiteSpace(text))
                {
                    throw new InvalidOperationException("Gemini response did not contain any text.");
                }

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

                suggestionsText = narrativePart;

                using var resultDoc = JsonDocument.Parse(jsonPart);
                var resultRoot = resultDoc.RootElement;

                geminiMatchScore = resultRoot.GetProperty("matchScore").GetString() ?? "Unknown";

                jdKeywordsFromGemini = resultRoot
                    .GetProperty("jdKeywords")
                    .EnumerateArray()
                    .Select(e => e.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                resumeKeywordsFromGemini = resultRoot
                    .GetProperty("resumeKeywords")
                    .EnumerateArray()
                    .Select(e => e.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                missingKeywordsFromGemini = resultRoot
                    .GetProperty("missingKeywords")
                    .EnumerateArray()
                    .Select(e => e.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                keywordsToAddFromGemini = resultRoot
                    .GetProperty("keywordsToAdd")
                    .EnumerateArray()
                    .Select(e => e.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Gemini] Failed to parse response JSON.");
                throw new InvalidOperationException("Failed to parse Gemini response.", ex);
            }
        }

        // Our own match score, based on Gemini's jdKeywords/resumeKeywords
        string ourMatchScore;
        if (jdKeywordsFromGemini == null || jdKeywordsFromGemini.Count == 0)
        {
            ourMatchScore = "Unknown";
        }
        else
        {
            var jdDistinct = jdKeywordsFromGemini.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var resumeDistinct = (resumeKeywordsFromGemini ?? new List<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var ourMissing = jdDistinct
                .Where(k => !resumeDistinct.Contains(k, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var presentCount = jdDistinct.Count - ourMissing.Count;
            var ratio = presentCount / (double)jdDistinct.Count;
            ourMatchScore = ratio switch
            {
                >= 0.7 => "High",
                >= 0.4 => "Medium",
                _ => "Low"
            };

            // Prefer ourMissing over the model's missingKeywords for deterministic behavior
            missingKeywordsFromGemini = ourMissing;
        }

        var result = new GeminiPersonalizationResult
        {
            UpdatedResumeText = suggestionsText,

            OurKeywordsToAdd = jdKeywordsFromGemini ?? new List<string>(),
            OurMissingKeywords = missingKeywordsFromGemini ?? new List<string>(),
            OurMatchScore = ourMatchScore,

            GeminiKeywordsToAdd = keywordsToAddFromGemini ?? new List<string>(),
            GeminiMissingKeywords = missingKeywordsFromGemini ?? new List<string>(),
            GeminiMatchScore = geminiMatchScore
        };

        return result;
    }

    private static IEnumerable<string> ExtractKeywords(string text)
    {
        var matches = Regex.Matches(text, "[A-Z][a-zA-Z0-9_]+");
        return matches
            .Select(m => m.Value)
            .Where(v => v.Length > 2)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
