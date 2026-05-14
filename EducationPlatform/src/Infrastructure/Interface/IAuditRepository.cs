using Domain.AuditManagement.Aggregate;

namespace Infrastructure.Interface
{
    public interface IAuditLogRepository :
        IGenericRepository<AuditLog>,
        IRepositoryBase
    {
    }
}

