using System.Threading.Tasks;

namespace Application.Interface
{
    public interface IEmailService
    {
        Task SendVerificationEmailAsync(string toEmail, string otp);
    }
}
