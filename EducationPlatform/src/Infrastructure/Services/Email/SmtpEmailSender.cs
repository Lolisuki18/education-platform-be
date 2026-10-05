using System.Net;
using System.Net.Mail;
using Application.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.Email
{
    public interface IEmailSender
    {
        /// <summary>Delivers one message. Failures are thrown so the caller can retry.</summary>
        Task SendAsync(EmailMessage email, CancellationToken cancellationToken);
    }

    /// <summary>Delivers messages over SMTP.</summary>
    public class SmtpEmailSender : IEmailSender
    {
        private readonly EmailOptions _options;

        public SmtpEmailSender(IOptions<EmailOptions> options)
        {
            _options = options.Value;
        }

        public async Task SendAsync(EmailMessage email, CancellationToken cancellationToken)
        {
            using var smtp = new SmtpClient
            {
                Host = _options.SmtpHost,
                Port = _options.SmtpPort,
                EnableSsl = _options.EnableSSL,
                Timeout = 10_000
            };

            if (!string.IsNullOrEmpty(_options.Username) && !string.IsNullOrEmpty(_options.Password))
            {
                smtp.Credentials = new NetworkCredential(_options.Username, _options.Password);
            }

            using var message = new MailMessage(
                new MailAddress(_options.From, _options.DisplayName),
                new MailAddress(email.To))
            {
                Subject = email.Subject,
                Body = email.Body,
                IsBodyHtml = true
            };

            await smtp.SendMailAsync(message, cancellationToken);
        }
    }
}
