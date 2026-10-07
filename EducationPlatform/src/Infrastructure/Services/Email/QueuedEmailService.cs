using Application.Interface;

namespace Infrastructure.Services.Email
{
    /// <summary>
    /// What the application talks to: it only puts the message on the queue, so a slow or broken SMTP server
    /// can no longer delay or fail a request. <see cref="EmailDispatchService"/> does the actual sending.
    /// </summary>
    public class QueuedEmailService : IEmailService
    {
        private readonly EmailQueue _queue;

        public QueuedEmailService(EmailQueue queue)
        {
            _queue = queue;
        }

        public Task SendVerificationEmailAsync(string toEmail, string otp)
        {
            _queue.Enqueue(new EmailMessage(toEmail, "Email Verification", BuildOtpTemplate(otp)));
            return Task.CompletedTask;
        }

        public Task SendPasswordResetEmailAsync(string toEmail, string otp)
        {
            _queue.Enqueue(new EmailMessage(toEmail, "Password Reset", BuildOtpTemplate(otp, "Reset Your Password", 10)));
            return Task.CompletedTask;
        }

        public Task SendPasswordChangedEmailAsync(string toEmail)
        {
            _queue.Enqueue(new EmailMessage(toEmail, "Your password was changed", BuildPasswordChangedTemplate(DateTime.UtcNow)));
            return Task.CompletedTask;
        }

        public Task SendEmailAsync(string toEmail, string subject, string body)
        {
            _queue.Enqueue(new EmailMessage(toEmail, subject, body));
            return Task.CompletedTask;
        }

        internal static string BuildPasswordChangedTemplate(DateTime whenUtc)
        {
            return $@"
            <!DOCTYPE html>
            <html>
            <body style='font-family:Segoe UI;background:#f9f9f9;padding:40px'>
                <div style='max-width:600px;margin:auto;background:#fff;
                            padding:20px;border-radius:10px'>
                    <h2 style='color:#2a7ae2'>Your password was changed</h2>
                    <p>The password of your account was changed on {whenUtc:yyyy-MM-dd HH:mm} UTC and you were signed out on all devices.</p>
                    <p><b>If this was you, nothing more is needed.</b></p>
                    <p>If it was not you, someone else may have access to your account: use ""Forgot password"" right away
                       to choose a new password, and contact support.</p>
                </div>
            </body>
            </html>";
        }

        internal static string BuildOtpTemplate(string otp, string title = "Verify Your Email", int validMinutes = 5)
        {
            return $@"
            <!DOCTYPE html>
            <html>
            <body style='font-family:Segoe UI;background:#f9f9f9;padding:40px'>
                <div style='max-width:600px;margin:auto;background:#fff;
                            padding:20px;border-radius:10px;text-align:center'>
                    <h2 style='color:#2a7ae2'>{title}</h2>
                    <p>Your OTP code:</p>
                    <div style='font-size:24px;font-weight:bold;
                                padding:10px 20px;
                                background:#f0f4ff;
                                display:inline-block'>
                        {otp}
                    </div>
                    <p style='font-size:13px;color:#888'>
                        This code expires in {validMinutes} minutes
                    </p>
                </div>
            </body>
            </html>";
        }
    }
}
