using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RecruiterOutreachConsole;
using RecruiterOutreachConsole.Emailing;
using RecruiterOutreachConsole.Gemini;
using RecruiterOutreachConsole.Outreach;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        var basePath = AppContext.BaseDirectory;
        config.SetBasePath(basePath);
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        var outreachSettings = configuration.Get<OutreachSettings>() ?? new OutreachSettings();

        services.AddSingleton(outreachSettings);
        services.AddSingleton(outreachSettings.SmtpSettings);
        services.AddSingleton(outreachSettings.Gemini);

        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IGeminiPersonalizationService, GeminiPersonalizationService>();
        services.AddSingleton<OutreachService>();
    });

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("RecruiterOutreachConsole");

// Use Console.WriteLine for the initial banner to avoid mixing with prompts.
Console.WriteLine("Recruiter Outreach Console starting up...");
Console.WriteLine();

Console.Write("Enter Company name: ");
var company = Console.ReadLine() ?? string.Empty;

Console.Write("Enter Role title: ");
var role = Console.ReadLine() ?? string.Empty;

Console.WriteLine();
Console.WriteLine("Paste Job Description (JD) if you want AI personalization, then press Enter on an empty line to finish. Leave empty to skip:");

string? line;
var jdWriter = new System.Text.StringBuilder();
while (!string.IsNullOrEmpty(line = Console.ReadLine()))
{
    jdWriter.AppendLine(line);
}

var jobDescription = jdWriter.ToString();
if (string.IsNullOrWhiteSpace(jobDescription))
{
    jobDescription = null;
}

var outreachService = host.Services.GetRequiredService<OutreachService>();

try
{
    await outreachService.RunAsync(company, role, jobDescription);
}
catch (Exception ex)
{
    logger.LogError(ex, "Unexpected error during outreach run.");
}

logger.LogInformation("Recruiter Outreach Console finished. Press any key to exit.");
Console.ReadKey();
