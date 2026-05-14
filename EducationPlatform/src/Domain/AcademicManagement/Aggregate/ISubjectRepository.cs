using Domain.Common.Interfaces;
using Domain.AcademicManagement.Aggregate;

namespace Domain.AcademicManagement.Aggregate
{
    public interface ISubjectRepository : IGenericRepository<Subject>
    {
        Task<IEnumerable<Subject>> GetSubjectsByGrade(Guid gradeId);
    }
}
