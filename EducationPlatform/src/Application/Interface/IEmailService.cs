using System.Threading.Tasks;

namespace Application.Interface
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string toEmail, string otp);
        Task SendPasswordResetEmailAsync(string toEmail, string otp);

        /// <summary>Tells the owner that the password was changed or reset, so they notice if it was not them.</summary>
        Task SendPasswordChangedEmailAsync(string toEmail);
        Task SendEmailAsync(string toEmail, string subject, string body);
    }
}
