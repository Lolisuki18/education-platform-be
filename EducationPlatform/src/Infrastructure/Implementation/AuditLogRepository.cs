using Domain.Common.Interfaces;
using Infrastructure.Persistence;
using Domain.AuditManagement.Aggregate;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Implementation
{
    public class AuditLogRepository :
        GenericRepository<AuditLog>,
        IAuditRepository

    {
        #region Attributes
        #endregion

        #region Properties
        #endregion

        public AuditLogRepository(EducationPlatformDBContext context) : base(context) { }

        #region Methods
        public async Task<int> DeleteUserEntriesAsync(Guid userId, DateTime before, CancellationToken cancellationToken = default)
        {
            // The audit values are JSON, in which a Guid is written in its lower-case hyphenated form
            var id = userId.ToString();

            return await context.AuditLogs
                .Where(a => a.EntityName == "User"
                            && a.Timestamp < before
                            && ((a.OldValue != null && a.OldValue.Contains(id)) || (a.NewValue != null && a.NewValue.Contains(id))))
                .ExecuteDeleteAsync(cancellationToken);
        }
        #endregion
    }
}

