using Application.Results;
using Application.Commands.Identity;

namespace Application.Interface
{
    public interface IIdentityService
    {
        Task<TokenDTO> Login(
            LoginDto dto);

        Task Register(
            RegisterDto dto);

        Task VerifyEmail(
            string otp);

        Task<TokenDTO> RefreshToken(
            string refreshToken);
    }
}


