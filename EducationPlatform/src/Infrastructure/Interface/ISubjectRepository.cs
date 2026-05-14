using Domain.AcademicManagement.Aggregate;
using Domain.AcademicManagement.Entity;

namespace Infrastructure.Interface
{
    public interface ISubjectRepository :
        IGenericRepository<Subject>,
        IRepositoryBase
    {
        Task<IEnumerable<DefaultLesson>> GetDefaultLessons(
            Guid subjectId,
            Guid gradeId);
    }
}

