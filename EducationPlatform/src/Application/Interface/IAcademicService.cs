using Application.Results;

namespace Application.Interface
{
    public interface IAcademicService
    {
        Task<IEnumerable<SubjectDTO>> GetSubjects();

        Task<IEnumerable<GradeDTO>> GetGrades();

        Task<IEnumerable<DefaultLessonDTO>> GetDefaultLessons(
            Guid subjectId,
            Guid gradeId);
    }
}


