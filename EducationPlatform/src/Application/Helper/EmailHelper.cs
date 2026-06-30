using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace Application.Helper
{
    public static class EmailHelper
    {
        private static IConfiguration? _configuration;
        private static IConfiguration Configuration
        {
            get
            {
                if (_configuration == null)
                {
                    _configuration = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: true)
                        .Build();
                }
                return _configuration;
            }
        }

        public static async Task SendVerificationEmailAsync(string toEmail, string otp)
        {
            try
            {
                var config = Configuration;
                var fromEmail = config["EmailSettings:From"] ?? "noreply@educationplatform.com";
                var displayName = config["EmailSettings:DisplayName"] ?? "Education Platform";
                var smtpHost = config["EmailSettings:SmtpHost"] ?? "localhost";
                var smtpPortStr = config["EmailSettings:SmtpPort"];
                var smtpPort = int.TryParse(smtpPortStr, out var port) ? port : 25;
                var username = config["EmailSettings:Username"] ?? string.Empty;
                var password = config["EmailSettings:Password"] ?? string.Empty;
                var enableSSLStr = config["EmailSettings:EnableSSL"];
                var enableSSL = bool.TryParse(enableSSLStr, out var ssl) && ssl;

                var from = new MailAddress(fromEmail, displayName);
                var to = new MailAddress(toEmail);

                using var smtp = new SmtpClient
                {
                    Host = smtpHost,
                    Port = smtpPort,
                    EnableSsl = enableSSL
                };

                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    smtp.Credentials = new NetworkCredential(username, password);
                }

                using var message = new MailMessage(from, to)
                {
                    Subject = "Email Verification",
                    Body = BuildOtpTemplate(otp),
                    IsBodyHtml = true
                };

                await smtp.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                // Prevent SMTP downtime or local test environment issues from crashing user registration/actions.
                // In production, failed emails should be queued or retried asynchronously.
                Console.WriteLine($"[EmailHelper] Failed to send email to {toEmail}: {ex.Message}");
            }
        }

        private static string BuildOtpTemplate(string otp)
        {
            return $@"
            <!DOCTYPE html>
            <html>
            <body style='font-family:Segoe UI;background:#f9f9f9;padding:40px'>
                <div style='max-width:600px;margin:auto;background:#fff;
                            padding:20px;border-radius:10px;text-align:center'>
                    <h2 style='color:#2a7ae2'>Verify Your Email</h2>
                    <p>Your OTP code:</p>
                    <div style='font-size:24px;font-weight:bold;
                                padding:10px 20px;
                                background:#f0f4ff;
                                display:inline-block'>
                        {otp}
                    </div>
                    <p style='font-size:13px;color:#888'>
                        This code expires in 5 minutes
                    </p>
                </div>
            </body>
            </html>";
        }
    }
}

