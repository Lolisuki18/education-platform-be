using Domain.IdentityManagement.Aggregate;

namespace Application.Interface
{
    public interface ITokenService
    {
        string GenerateToken(User user);
        string GenerateRefreshToken();
    }
}
