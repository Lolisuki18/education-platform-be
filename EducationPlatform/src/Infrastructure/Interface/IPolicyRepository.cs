using Domain.CourseManagement.Aggregate;

namespace Infrastructure.Interface
{
    public interface IPolicyRepository :
        IGenericRepository<Policy>,
        IRepositoryBase
    {
        Task<IEnumerable<Policy>> GetDetailPolicies();
    }
}

