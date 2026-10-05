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

        public Task SendEmailAsync(string toEmail, string subject, string body)
        {
            _queue.Enqueue(new EmailMessage(toEmail, subject, body));
            return Task.CompletedTask;
        }

        internal static string BuildOtpTemplate(string otp)
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
