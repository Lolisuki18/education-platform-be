using Domain.Common.Interfaces;
using Domain.AcademicManagement.Aggregate;

namespace Domain.AcademicManagement.Aggregate
{
    public interface IGradeRepository : IGenericRepository<Grade>
    {
        Task<bool> IsInUse(Guid gradeId, CancellationToken cancellationToken = default);

        /// <summary>Whether another grade already has this name (ignoring case). <paramref name="excludeGradeId"/> is the grade being renamed.</summary>
        Task<bool> NameExistsAsync(string name, Guid? excludeGradeId = null, CancellationToken cancellationToken = default);
    }
}
