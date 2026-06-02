using System.Text.Json;
using RecruiterOutreach.Core;
using RecruiterOutreach.Core.Emailing;
using RecruiterOutreach.Core.Gemini;
using RecruiterOutreach.Core.Config;
using Google.GenAI;
using RecruiterOutreach.Core.Outreach;
using RecruiterOutreach.Api.Endpoints;
using RecruiterOutreach.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Load OutreachSettings from the named section in configuration
var outreachSettings = builder.Configuration
    .GetSection("OutreachSettings")
    .Get<OutreachSettings>() ?? new OutreachSettings();

// Optionally load Gemini multi-model configuration from Config/gemini.json
var baseDir = AppContext.BaseDirectory;
var configDir = Path.Combine(baseDir, "Config");
var geminiConfigPath = Path.Combine(configDir, "gemini.json");
GeminiConfig? geminiConfig = null;
if (File.Exists(geminiConfigPath))
{
    try
    {
        var geminiJson = File.ReadAllText(geminiConfigPath);
        geminiConfig = JsonSerializer.Deserialize<GeminiConfig>(geminiJson);
    }
    catch
    {
        // Ignore malformed Gemini config; we'll fall back to defaults / single-model config if needed.
    }
}

// Load resume suggestion prompt from Config/Prompts/resume.json, probing common locations
var promptsDir = Path.Combine(configDir, "Prompts");
var candidates = new List<string>();
// 1) API base dir: bin/.../Config/Prompts/resume.json
candidates.Add(Path.Combine(promptsDir, "resume.json"));
// 2) Content root: <repo>/RecruiterOutreach.Api/Config/Prompts/resume.json
candidates.Add(Path.Combine(builder.Environment.ContentRootPath, "Config", "Prompts", "resume.json"));
// 3) Core project: <repo>/RecruiterOutreach.Core/Config/Prompts/resume.json
var coreConfig = Path.Combine(builder.Environment.ContentRootPath, "..", "RecruiterOutreach.Core", "Config", "Prompts", "resume.json");
candidates.Add(Path.GetFullPath(coreConfig));

string? foundResumePrompt = candidates.FirstOrDefault(File.Exists);
if (!string.IsNullOrWhiteSpace(foundResumePrompt))
{
    try
    {
        var promptJson = File.ReadAllText(foundResumePrompt);
        using var doc = JsonDocument.Parse(promptJson);
        var root = doc.RootElement;
        if (root.TryGetProperty("Template", out var templateProp) && templateProp.ValueKind == JsonValueKind.String)
        {
            var template = templateProp.GetString();
            if (!string.IsNullOrWhiteSpace(template))
            {
                outreachSettings.Gemini.ResumeSuggestionPromptTemplate = template;
            }
        }
    }
    catch
    {
        // Ignore malformed prompt files; Gemini service will enforce correctness at runtime.
    }
}

// Load role email templates from JSON files (if present)
var templatesFolder = Path.Combine(AppContext.BaseDirectory, "EmailTemplates");
if (Directory.Exists(templatesFolder))
{
    foreach (var file in Directory.GetFiles(templatesFolder, "*.json", SearchOption.TopDirectoryOnly))
    {
        try
        {
            var json = File.ReadAllText(file);
            var template = JsonSerializer.Deserialize<RoleEmailTemplateSettings>(json);
            if (template is not null && !string.IsNullOrWhiteSpace(template.Key))
            {
                outreachSettings.RoleEmailTemplates.RemoveAll(t => string.Equals(t.Key, template.Key, StringComparison.OrdinalIgnoreCase));
                outreachSettings.RoleEmailTemplates.Add(template);
            }
        }
        catch
        {
            // Ignore malformed template files; they can be fixed or removed without breaking startup.
        }
    }
}

// Register core settings and services
builder.Services.AddSingleton(outreachSettings);
builder.Services.AddSingleton(outreachSettings.SmtpSettings);
builder.Services.AddSingleton(outreachSettings.Gemini);
builder.Services.AddSingleton(outreachSettings.Scoring);
if (geminiConfig is not null)
{
    builder.Services.AddSingleton(geminiConfig);
}

builder.Services.AddLogging();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<InMemoryTokenStore>();
builder.Services.AddSingleton<IEmailSender, GmailApiEmailSender>();

builder.Services.AddSingleton(sp =>
{
    var geminiSettings = sp.GetRequiredService<GeminiSettings>();
    // Prefer explicit key from settings, fall back to environment variable used by Google.GenAI
    var apiKey = !string.IsNullOrWhiteSpace(geminiSettings.ApiKey)
        ? geminiSettings.ApiKey
        : Environment.GetEnvironmentVariable("GEMINI_API_KEY");

    return string.IsNullOrWhiteSpace(apiKey)
        ? new Client()
        : new Client(apiKey: apiKey);
});

builder.Services.AddSingleton<IGeminiPersonalizationService, GeminiPersonalizationService>();
builder.Services.AddSingleton<OutreachService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Endpoint groups
app.MapEmailSenderApi();
app.MapAiSuggestionsApi();
app.MapIdentityApi();

app.Run();
