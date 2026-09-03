using ECommerce.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerce.Infrastructure.Services;

public class EmailSender : IEmailSender
{
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(ILogger<EmailSender> logger)
    {
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        // Simulate sending email
        _logger.LogInformation("Sending email to {To} subject {Subject}", to, subject);
        await Task.Delay(500, cancellationToken);
        _logger.LogInformation("Email sent to {To}", to);
    }
}
