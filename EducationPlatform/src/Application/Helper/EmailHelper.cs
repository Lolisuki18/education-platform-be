using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace Application.Helper
{
    public static class EmailHelper
    {
        private static readonly IConfiguration Configuration =
            new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false)
                .Build();

        private static readonly string FromEmail = Configuration["EmailSettings:From"]!;
        private static readonly string DisplayName = Configuration["EmailSettings:DisplayName"]!;
        private static readonly string SmtpHost = Configuration["EmailSettings:SmtpHost"]!;
        private static readonly int SmtpPort = int.Parse(Configuration["EmailSettings:SmtpPort"]!);
        private static readonly string Username = Configuration["EmailSettings:Username"]!;
        private static readonly string Password = Configuration["EmailSettings:Password"]!;
        private static readonly bool EnableSSL = bool.Parse(Configuration["EmailSettings:EnableSSL"]!);

        public static async Task SendVerificationEmailAsync(string toEmail, string otp)
        {
            var from = new MailAddress(FromEmail, DisplayName);
            var to = new MailAddress(toEmail);

            using var smtp = new SmtpClient
            {
                Host = SmtpHost,
                Port = SmtpPort,
                EnableSsl = EnableSSL,
                Credentials = new NetworkCredential(Username, Password)
            };

            using var message = new MailMessage(from, to)
            {
                Subject = "Email Verification",
                Body = BuildOtpTemplate(otp),
                IsBodyHtml = true
            };

            await smtp.SendMailAsync(message);
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

