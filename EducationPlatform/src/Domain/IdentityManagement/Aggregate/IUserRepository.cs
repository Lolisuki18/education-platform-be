using Domain.Common.Interfaces;

namespace Domain.IdentityManagement.Aggregate
{
    public interface IUserRepository : IGenericRepository<User>
    {
        Task<User?> GetUserByEmail(string email);
        Task<User?> GetUserByPhone(string phone);
        Task<User?> GetUserForLogin(string email, string password);
        Task<User?> GetUserForRefreshToken(string refreshToken);
        Task<User?> GetUserForVerification(string email, string verificationCode);
        
        // Aliases for Application layer consistency
        Task<User?> GetUserByOTP(string otp);
        Task<User?> GetByRefreshToken(string refreshToken);

        Task<(int TotalUsers, int TotalTeachers, int TotalStudents)> Summary(
            DateTime? from, 
            DateTime? to);

        Task<Dictionary<string, List<(string Label, decimal Value)>>> AnalyticsGrowth(
            DateTime? from, 
            DateTime? to, 
            string groupBy,
            string? role = null);
    }
}
