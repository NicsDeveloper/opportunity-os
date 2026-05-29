using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using OpportunityOS.Application.Digest;

namespace OpportunityOS.Infrastructure.Email;

public sealed class EmailOptions
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool UseSsl { get; set; } = true;
}

/// <summary>SMTP transport via System.Net.Mail. Inactive until Host + To are set.</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(EmailOptions options, ILogger<SmtpEmailSender> logger)
    {
        _options = options;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Smtp.Host) && !string.IsNullOrWhiteSpace(_options.To);

    public async Task SendAsync(string subject, string htmlBody, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("SMTP is not configured (Email:Smtp:Host / Email:To).");

        using var message = new MailMessage
        {
            From = new MailAddress(string.IsNullOrWhiteSpace(_options.From) ? _options.Smtp.Username : _options.From),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(_options.To);

        using var client = new SmtpClient(_options.Smtp.Host, _options.Smtp.Port)
        {
            EnableSsl = _options.Smtp.UseSsl,
            Credentials = string.IsNullOrWhiteSpace(_options.Smtp.Username)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_options.Smtp.Username, _options.Smtp.Password)
        };

        _logger.LogInformation("Sending digest e-mail to {To} via {Host}:{Port}", _options.To, _options.Smtp.Host, _options.Smtp.Port);
        await client.SendMailAsync(message, cancellationToken);
    }
}
