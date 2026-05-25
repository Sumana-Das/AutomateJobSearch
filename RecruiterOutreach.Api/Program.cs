using Microsoft.AspNetCore.Mvc;
using RecruiterOutreach.Api.Contracts;
using RecruiterOutreach.Core;
using RecruiterOutreach.Core.Emailing;
using RecruiterOutreach.Core.Gemini;
using RecruiterOutreach.Core.Outreach;

var builder = WebApplication.CreateBuilder(args);

// Load OutreachSettings from configuration
var outreachSettings = builder.Configuration.Get<OutreachSettings>() ?? new OutreachSettings();

// Register core settings and services
builder.Services.AddSingleton(outreachSettings);
builder.Services.AddSingleton(outreachSettings.SmtpSettings);
builder.Services.AddSingleton(outreachSettings.Gemini);

builder.Services.AddLogging();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
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

// Suggestions endpoint (JD-based, no send)
app.MapPost("/api/outreach/suggestions", async (
    [FromBody] SuggestionsRequest request,
    IGeminiPersonalizationService gemini) =>
{
    var personalization = await gemini.PersonalizeAsync(string.Empty, request.JobDescription);

    var jdExcerpt = request.JobDescription.Length > 1000
        ? request.JobDescription.Substring(0, 1000) + "..."
        : request.JobDescription;

    var response = new SuggestionsResponse(
        personalization.UpdatedResumeText,
        personalization.KeywordsToAdd,
        jdExcerpt);

    return Results.Ok(response);
})
.WithName("GetSuggestions");

// Basic send endpoint (no JD-based personalization; uses default attachment)
app.MapPost("/api/outreach/send", async (
    [FromForm] string company,
    [FromForm] string role,
    OutreachService outreach) =>
{
    await outreach.RunAsync(company, role, jobDescription: null);
    return Results.Ok();
})
.WithName("SendEmails");

app.Run();
