using System.Threading.Tasks;

namespace Application.Interface
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string toEmail, string otp);
        Task SendEmailAsync(string toEmail, string subject, string body);
    }
}
