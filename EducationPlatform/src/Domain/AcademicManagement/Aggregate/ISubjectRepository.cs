using Domain.Common.Interfaces;
using Domain.AcademicManagement.Aggregate;
using Domain.AcademicManagement.Entity;

namespace Domain.AcademicManagement.Aggregate
{
    public interface ISubjectRepository : IGenericRepository<Subject>
    {
        Task<IEnumerable<Subject>> GetSubjectsByGrade(Guid gradeId);
        Task<IEnumerable<DefaultLesson>> GetDefaultLessons(Guid subjectId, Guid gradeId);
    }
}
