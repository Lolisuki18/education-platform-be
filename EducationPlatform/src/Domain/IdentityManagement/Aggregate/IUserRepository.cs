using Domain.Common.Interfaces;
using Domain.IdentityManagement.Enum;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Domain.IdentityManagement.Aggregate
{
    public interface IUserRepository : IGenericRepository<User>
    {
        Task<User?> GetUserByEmail(string email, CancellationToken cancellationToken = default);
        Task<User?> GetUserByPhone(string phone, CancellationToken cancellationToken = default);

        /// <summary>Finds the user that owns a refresh token, whether the session is still active or not.</summary>
        Task<User?> GetByRefreshToken(string refreshToken, CancellationToken cancellationToken = default);
        Task<User?> GetByIdWithSessions(Guid userId, CancellationToken cancellationToken = default);

        Task<(int TotalUsers, int TotalTeachers, int TotalStudents)> Summary(
            DateTime? from,
            DateTime? to,
            CancellationToken cancellationToken = default);

        Task<Dictionary<string, List<(string Label, decimal Value)>>> AnalyticsGrowth(
            DateTime? from,
            DateTime? to,
            string groupBy,
            string? role = null,
            CancellationToken cancellationToken = default);

        Task<(IEnumerable<User> Users, int TotalCount)> GetUsersPaged(int pageIndex, int pageSize, Role? role, CancellationToken cancellationToken = default);
    }
}
