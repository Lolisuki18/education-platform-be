using Domain.Common.Interfaces;
using Domain.AuditManagement.Aggregate;

namespace Domain.AuditManagement.Aggregate
{
    public interface IAuditRepository : IGenericRepository<AuditLog>
    {
    }
}
