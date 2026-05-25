using System.Threading;
using System.Threading.Tasks;

namespace RecruiterOutreachConsole.Emailing;

public interface IEmailSender
{
    Task SendEmailAsync(
        string to,
        string subject,
        string body,
        string? attachmentPath,
        CancellationToken cancellationToken = default);
}
