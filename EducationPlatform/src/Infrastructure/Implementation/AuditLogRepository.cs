using Domain.Common.Interfaces;
using Infrastructure.Persistence;
using Domain.AuditManagement.Aggregate;

namespace Infrastructure.Implementation
{
    public class AuditLogRepository :
        GenericRepository<AuditLog>,
        IAuditLogRepository
    {
        #region Attributes
        #endregion

        #region Properties
        #endregion

        public AuditLogRepository(EducationPlatformDBContext context) : base(context) { }

        #region Methods
        #endregion
    }
}

