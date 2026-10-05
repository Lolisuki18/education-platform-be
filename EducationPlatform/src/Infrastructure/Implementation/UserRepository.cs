using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Domain.IdentityManagement.Entity;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Implementation
{
    public class UserRepository :
        GenericRepository<User>,
        IUserRepository
    {
        public UserRepository(EducationPlatformDBContext context) : base(context) { }

        #region Methods
        public async Task<User?> GetUserByEmail(string email, CancellationToken cancellationToken = default)
        {
            return await context.Users
                .Include(u => u.RefreshSessions)
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        }

        public async Task<User?> GetUserByPhone(string phone, CancellationToken cancellationToken = default)
        {
            return await context.Users.FirstOrDefaultAsync(u => u.Phone == phone, cancellationToken);
        }

        public async Task<User?> GetByRefreshToken(string refreshToken, CancellationToken cancellationToken = default)
        {
            // Revoked and expired sessions are matched too, so the domain can tell a replayed token from an unknown one.
            var hash = RefreshSession.HashToken(refreshToken);
            return await context.Users
                .Include(u => u.RefreshSessions)
                .FirstOrDefaultAsync(u => u.RefreshSessions.Any(s => s.Hash == hash), cancellationToken);
        }

        public async Task<List<Guid>> GetUserIdsByRoleAsync(Role role, CancellationToken cancellationToken = default)
        {
            return await context.Users
                .AsNoTracking()
                .Where(u => u.Role == role && u.IsActive)
                .Select(u => u.UserID)
                .ToListAsync(cancellationToken);
        }

        public async Task<User?> GetByIdWithSessions(Guid userId, CancellationToken cancellationToken = default)
        {
            return await context.Users
                .Include(u => u.RefreshSessions)
                .FirstOrDefaultAsync(u => u.UserID == userId, cancellationToken);
        }

        public async Task<(int TotalUsers, int TotalTeachers, int TotalStudents)> Summary(DateTime? from, DateTime? to, CancellationToken cancellationToken = default)
        {
            var query = context.Users.AsNoTracking();

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
                .FirstOrDefaultAsync(cancellationToken);

            return result == null
                ? (0, 0, 0)
                : (result.TotalUsers, result.TotalTeachers, result.TotalStudents);
        }

        public async Task<Dictionary<string, List<(string Label, decimal Value)>>> AnalyticsGrowth(
            DateTime? from,
            DateTime? to,
            string groupBy,
            string? userRole = null,
            CancellationToken cancellationToken = default)
        {
            var query = context.Users.AsNoTracking();

            if (from.HasValue)
                query = query.Where(u => u.CreatedAt >= from.Value);

            if (to.HasValue)
                query = query.Where(u => u.CreatedAt <= to.Value);

            if (!string.IsNullOrEmpty(userRole))
                query = query.Where(u => u.Role.ToString() == userRole);

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
                .ToListAsync(cancellationToken);

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

            var seriesName = string.IsNullOrEmpty(userRole) ? "Users" : userRole;

            return new Dictionary<string, List<(string, decimal)>>
            {
                {
                    seriesName,
                    data.Select(x => (x.Label, (decimal)x.Count)).ToList()
                }
            };
        }

        public async Task<(IEnumerable<User> Users, int TotalCount)> GetUsersPaged(int pageIndex, int pageSize, Role? role, CancellationToken cancellationToken = default)
        {
            var query = context.Users.AsNoTracking();

            if (role.HasValue)
            {
                query = query.Where(u => u.Role == role.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var list = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (list, totalCount);
        }
        #endregion
    }
}
