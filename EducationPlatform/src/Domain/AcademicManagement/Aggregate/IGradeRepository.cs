using Domain.Common.Interfaces;
using Domain.AcademicManagement.Aggregate;

namespace Domain.AcademicManagement.Aggregate
{
    public interface IGradeRepository : IGenericRepository<Grade>
    {
        Task<bool> IsInUse(Guid gradeId);
    }
}
