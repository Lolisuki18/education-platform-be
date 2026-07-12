using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Numerics;

namespace Infrastructure.Implementation
{
    public class UserRepository :
        GenericRepository<User>,
        IUserRepository
    {
        #region Attributes
        #endregion

        #region Properties
        #endregion

        public UserRepository(EducationPlatformDBContext context) : base(context) { }

        #region Methods
        public async Task<User?> GetUserByEmail(string email)
        {
            return await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User?> GetUserByPhone(string phone)
        {
            return await context.Users.FirstOrDefaultAsync(u => u.Phone == phone);
        }

        public async Task<User?> GetByRefreshToken(string refreshToken)
        {
            var users = await context.Users
                .Where(u => u.RefreshToken != null)
                .ToListAsync();

            return users.FirstOrDefault(u =>
                u.RefreshToken.Verify(refreshToken));
        }

        public async Task<User?> GetUserByOTP(string otp)
        {
            return await context.Users
                .FirstOrDefaultAsync(u => u.EmailOtp == otp);
        }

        public async Task<User?> GetUserForLogin(string email, string password)
        {
            var user = await GetUserByEmail(email);
            if (user == null || !user.Password.Verify(password))
                return null;
            return user;
        }

        public async Task<User?> GetUserForRefreshToken(string refreshToken)
        {
            return await GetByRefreshToken(refreshToken);
        }

        public async Task<User?> GetUserForVerification(string email, string verificationCode)
        {
            return await context.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.EmailOtp == verificationCode);
        }

        public async Task<(int TotalUsers, int TotalTeachers, int TotalStudents)> Summary(DateTime? from, DateTime? to)
        {
            var query = context.Users.AsQueryable();

            // ===== Apply date filters =====
            if (from.HasValue)
                query = query.Where(u => u.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(u => u.CreatedAt <= to.Value);

            var result = await query
                .GroupBy(u => 1)
                .Select(g => new
                {
                    TotalUsers = g.Count(),
                    TotalTeachers = g.Count(u => u.Role == Role.Teacher),
                    TotalStudents = g.Count(u => u.Role == Role.Student)
                })
                .FirstOrDefaultAsync();

            return result == null
                ? (0, 0, 0)
                : (result.TotalUsers, result.TotalTeachers, result.TotalStudents);
        }

        public async Task<Dictionary<string, List<(string Label, decimal Value)>>> AnalyticsGrowth(
            DateTime? from,
            DateTime? to,
            string groupBy,
            string? userRole)
        {
            var query = context.Users.AsQueryable();

            // ===== Filters =====
            if (from.HasValue)
                query = query.Where(u => u.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(u => u.CreatedAt <= to.Value);

            if (!string.IsNullOrEmpty(userRole))
                query = query.Where(u => u.Role.ToString() == userRole);

            // ===== Step 1: Group in DB =====
            var rawData = await query
                .GroupBy(u => new
                {
                    u.CreatedAt.Year,
                    u.CreatedAt.Month,
                    u.CreatedAt.Day
                })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    g.Key.Day,
                    Count = g.Count()
                })
                .ToListAsync();

            // ===== Step 2: Format labels in memory =====
            var data = rawData
                .Select(x => new
                {
                    Label = groupBy.ToLower() switch
                    {
                        "day" => $"{x.Year}-{x.Month:D2}-{x.Day:D2}",
                        "month" => $"{x.Year}-{x.Month:D2}",
                        "year" => x.Year.ToString(),
                        _ => $"{x.Year}-{x.Month:D2}"
                    },
                    x.Count
                })
                .OrderBy(x => x.Label)
                .ToList();

            // ===== Build result =====
            var seriesName = string.IsNullOrEmpty(userRole) ? "Users" : userRole;

            return new Dictionary<string, List<(string, decimal)>>
            {
                {
                    seriesName,
                    data.Select(x => (x.Label, (decimal)x.Count)).ToList()
                }
            };
        }

        public async Task<(IEnumerable<User> Users, int TotalCount)> GetUsersPaged(int pageIndex, int pageSize, Role? role)
        {
            var query = context.Users.AsQueryable();

            if (role.HasValue)
            {
                query = query.Where(u => u.Role == role.Value);
            }

            var totalCount = await query.CountAsync();
            var list = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (list, totalCount);
        }
        #endregion
    }
}

