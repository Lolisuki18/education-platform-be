using Domain.Common.Interfaces;
using Domain.AuditManagement.Aggregate;

namespace Domain.AuditManagement.Aggregate
{
    public interface IAuditRepository : IGenericRepository<AuditLog>
    {
        /// <summary>
        /// Deletes (immediately) the audit rows about a user's own record written before <paramref name="before"/>.
        /// Used when the account is erased: older rows may still hold the e-mail address and phone number.
        /// </summary>
        Task<int> DeleteUserEntriesAsync(Guid userId, DateTime before, CancellationToken cancellationToken = default);
    }
}
