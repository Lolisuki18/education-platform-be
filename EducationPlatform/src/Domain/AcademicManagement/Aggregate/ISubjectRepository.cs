using Domain.Common.Interfaces;
using Domain.AcademicManagement.Aggregate;
using Domain.AcademicManagement.Entity;

namespace Domain.AcademicManagement.Aggregate
{
    public interface ISubjectRepository : IGenericRepository<Subject>
    {
        Task<IEnumerable<Subject>> GetSubjectsByGrade(Guid gradeId, CancellationToken cancellationToken = default);
        Task<IEnumerable<DefaultLesson>> GetDefaultLessons(Guid subjectId, Guid gradeId, CancellationToken cancellationToken = default);
        Task<bool> IsInUse(Guid subjectId, CancellationToken cancellationToken = default);

        /// <summary>Whether another subject already has this code (ignoring case). <paramref name="excludeSubjectId"/> is the subject being edited.</summary>
        Task<bool> CodeExistsAsync(string code, Guid? excludeSubjectId = null, CancellationToken cancellationToken = default);
    }
}
