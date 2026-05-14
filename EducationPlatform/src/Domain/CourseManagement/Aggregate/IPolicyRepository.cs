using Domain.Common.Interfaces;
using Domain.CourseManagement.Entity;

namespace Domain.CourseManagement.Aggregate
{
    public interface IPolicyRepository : IGenericRepository<PolicyRule>
    {
    }
}
