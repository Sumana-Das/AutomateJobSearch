using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace RecruiterOutreach.Core.Utilities;

/// <summary>
/// Shared utility for converting resume files and uploads into plain text, including PDF extraction via PdfPig.
/// </summary>
public static class ResumeTextExtractor
{
    public static async Task<string> ExtractFromFileAsync(string path, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();

        if (extension == ".pdf")
        {
            await using var stream = File.OpenRead(path);
            return ExtractTextFromPdf(stream);
        }

        return await File.ReadAllTextAsync(path, cancellationToken);
    }

    public static async Task<string> ExtractFromStreamAsync(Stream stream, string fileExtension, CancellationToken cancellationToken)
    {
        var extension = fileExtension.ToLowerInvariant();

        if (extension == ".pdf")
        {
            return ExtractTextFromPdf(stream);
        }

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    private static string ExtractTextFromPdf(Stream stream)
    {
        using var document = PdfDocument.Open(stream);
        var builder = new System.Text.StringBuilder();

        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return builder.ToString();
    }
}
