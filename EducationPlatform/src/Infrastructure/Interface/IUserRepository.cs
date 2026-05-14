using Domain.IdentityManagement.Aggregate;

namespace Infrastructure.Interface
{
    public interface IUserRepository :
        IGenericRepository<User>,
        IRepositoryBase
    {
        Task<User?> GetUserByEmail(string email);
        Task<User?> GetUserByPhone(string phone);
        Task<User?> GetByRefreshToken(string refreshToken);
        Task<User?> GetUserByOTP(string otp);

        Task<(int TotalUsers, int TotalTeachers, int TotalStudents)> Summary(DateTime? from, DateTime? to);

        Task<Dictionary<string, List<(string Label, decimal Value)>>> AnalyticsGrowth(
            DateTime? from,
            DateTime? to,
            string groupBy,
            string? userRole);
    }
}

