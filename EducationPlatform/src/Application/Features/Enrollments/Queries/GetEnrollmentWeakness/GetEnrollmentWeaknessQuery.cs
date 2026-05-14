using Application.Results;
using MediatR;
using Domain.Common.Interfaces;
using Application.BusinessException;

namespace Application.Features.Enrollments.Queries.GetEnrollmentWeakness
{
    public class GetEnrollmentWeaknessQuery : IRequest<IEnumerable<StudentWeaknessDTO>>
    {
        public Guid EnrollmentID { get; set; }
    }

    public class GetEnrollmentWeaknessQueryHandler : IRequestHandler<GetEnrollmentWeaknessQuery, IEnumerable<StudentWeaknessDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetEnrollmentWeaknessQueryHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<StudentWeaknessDTO>> Handle(GetEnrollmentWeaknessQuery request, CancellationToken cancellationToken)
        {
            var enrollment = await _unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .GetEnrollmentStatistic(request.EnrollmentID);

            if (enrollment == null)
                throw new NotFound("Student statistic not found");

            var weakness = new List<StudentWeaknessDTO>();

            if (enrollment.CourseProgress?.ChapterProgresses == null)
                return weakness;

            foreach (var chapter in enrollment.CourseProgress.ChapterProgresses)
            {
                foreach (var lesson in chapter.LessonProgresses)
                {
                    var failedQuiz = lesson.QuizProgresses
                        .Count(q => !q.IsCorrect && q.AttemptCount > 0);

                    if (failedQuiz == 0)
                        continue;

                    weakness.Add(new StudentWeaknessDTO
                    {
                        CourseId = enrollment.CourseID,
                        CourseTitle = enrollment.Course?.Title ?? "",
                        ChapterId = chapter.ChapterID,
                        LessonId = lesson.LessonID,
                        LessonTitle = lesson.Lesson?.Title ?? "",
                        // Note: lesson.CalculateCorrectQuizRate() is an extension or domain method
                        // We should ensure it's accessible or move it to a helper.
                        // Assuming it's a domain method on LessonProgress entity.
                        CompletionRate = (decimal)lesson.QuizProgresses.Count(q => q.IsCorrect) / lesson.QuizProgresses.Count,
                        FailedQuizCount = failedQuiz
                    });
                }
            }

            return weakness;
        }
    }
}
