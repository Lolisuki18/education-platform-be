using System.Collections.Concurrent;
using Application.Interface;

namespace IntegrationTests
{
    /// <summary>
    /// Verification codes are only e-mailed (the database keeps a hash), so tests read them from here instead of
    /// the users table. Static on purpose: derived factories (WithWebHostBuilder) must share one store.
    /// </summary>
    public static class TestEmailCapture
    {
        private static readonly ConcurrentDictionary<string, string> LastOtpByEmail = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentQueue<(string To, string Subject, string Body)> Sent = new();

        private static readonly ConcurrentDictionary<string, string> LastResetOtpByEmail = new(StringComparer.OrdinalIgnoreCase);

        public static void RecordOtp(string email, string otp) => LastOtpByEmail[email] = otp;

        public static void RecordResetOtp(string email, string otp) => LastResetOtpByEmail[email] = otp;

        public static string GetResetOtp(string email) =>
            LastResetOtpByEmail.TryGetValue(email, out var otp)
                ? otp
                : throw new InvalidOperationException($"No password reset code was e-mailed to {email}.");

        public static void Record(string to, string subject, string body) => Sent.Enqueue((to, subject, body));

        public static string GetOtp(string email) =>
            LastOtpByEmail.TryGetValue(email, out var otp)
                ? otp
                : throw new InvalidOperationException($"No verification code was e-mailed to {email}.");

        public static IReadOnlyCollection<(string To, string Subject, string Body)> SentEmails => Sent.ToArray();
    }

    public class CapturingEmailService : IEmailService
    {
        public Task SendVerificationEmailAsync(string toEmail, string otp)
        {
            TestEmailCapture.RecordOtp(toEmail, otp);
            return Task.CompletedTask;
        }

        public Task SendPasswordResetEmailAsync(string toEmail, string otp)
        {
            TestEmailCapture.RecordResetOtp(toEmail, otp);
            return Task.CompletedTask;
        }

        public Task SendEmailAsync(string toEmail, string subject, string body)
        {
            TestEmailCapture.Record(toEmail, subject, body);
            return Task.CompletedTask;
        }
    }
}
