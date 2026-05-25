using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace RecruiterOutreachConsole.Outreach;

/// <summary>
/// Helper for reading a DOCX resume template and writing updated DOCX resumes.
/// </summary>
public sealed class ResumeService
{
    public Task<string> ReadResumeTextAsync(string templatePath, CancellationToken cancellationToken = default)
    {
        // Open the DOCX and extract all paragraph text.
        var sb = new StringBuilder();

        using (var doc = WordprocessingDocument.Open(templatePath, false))
        {
            var body = doc.MainDocumentPart?.Document.Body;
            if (body != null)
            {
                foreach (var para in body.Elements<Paragraph>())
                {
                    var text = para.InnerText;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }
            }
        }

        return Task.FromResult(sb.ToString());
    }

    public Task<string> SaveUpdatedResumeAsync(
        string updatedText,
        string company,
        string role,
        string outputFolder,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            outputFolder = Path.GetDirectoryName(Environment.ProcessPath!) ?? Directory.GetCurrentDirectory();
        }

        Directory.CreateDirectory(outputFolder);

        var safeCompany = MakeSafeFileNamePart(company);
        var safeRole = MakeSafeFileNamePart(role);
        var fileName = $"UpdatedResume_{safeCompany}_{safeRole}_{DateTime.Now:yyyyMMddHHmmss}.docx";

        var fullPath = Path.Combine(outputFolder, fileName);

        // Create a very simple DOCX with the updated text as paragraphs.
        using (var doc = WordprocessingDocument.Create(fullPath, WordprocessingDocumentType.Document))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = new Body();

            using var reader = new StringReader(updatedText);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var para = new Paragraph(new Run(new Text(line)));
                body.AppendChild(para);
            }

            mainPart.Document.Append(body);
            mainPart.Document.Save();
        }

        return Task.FromResult(fullPath);
    }

    private static string MakeSafeFileNamePart(string input)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            input = input.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(input) ? "Unknown" : input;
    }
}
