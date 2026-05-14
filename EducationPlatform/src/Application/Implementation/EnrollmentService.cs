using AutoMapper;
using Application.BusinessException;
using Application.Results;
using Application.Interface;
using Infrastructure.Interface;

namespace Application.Implementation
{
    public class EnrollmentService : IEnrollmentService
    {
        #region Attributes
        private readonly IMapper mapper;
        private readonly IUnitOfWork unitOfWork;
        #endregion

        #region Properties
        #endregion

        public EnrollmentService(
            IMapper mapper,
            IUnitOfWork unitOfWork)
        {
            this.mapper = mapper;
            this.unitOfWork = unitOfWork;
        }

        #region Methods
        public async Task<IEnumerable<EnrollmentDTO>> GetStudentEnrollments(Guid studentId)
        {
            var list = await unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .GetStudentEnrollments(studentId);

            if (list == null || !list.Any())
                throw new NotFound("Enrollment list is not found or empty");

            return mapper.Map<IEnumerable<EnrollmentDTO>>(list);
        }

        public async Task<EnrollmentDetailDTO> GetEnrollmentDetail(Guid enrollmentId)
        {
            var enrollment = await unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .GetEnrollmentDetailByID(enrollmentId);

            if (enrollment == null)
                throw new NotFound("Enrollment detail has not been found");

            var dto = mapper.Map<EnrollmentDetailDTO>(enrollment);

            // Hide quiz answers
            if (dto.CourseProgress?.ChapterProgresses != null)
            {
                foreach (var chapter in dto.CourseProgress.ChapterProgresses)
                {
                    if (chapter.LessonProgresses == null)
                        continue;

                    foreach (var lesson in chapter.LessonProgresses)
                    {
                        if (lesson.QuizProgresses == null)
                            continue;

                        foreach (var quizProgress in lesson.QuizProgresses)
                        {
                            if (quizProgress.Quiz?.Answer != null)
                            {
                                quizProgress.Quiz.Answer.CorrectAnswers = new List<string>();
                            }
                        }
                    }
                }
            }

            return dto;
        }

        public async Task<IEnumerable<StudentWeaknessDTO>> GetEnrollmentWeakness(Guid enrollmentId)
        {
            var enrollment = await unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .GetEnrollmentStatistic(enrollmentId);

            if (enrollment == null)
                throw new NotFound("Student statistic is not found");

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
                        CompletionRate = lesson.CalculateCorrectQuizRate(),
                        FailedQuizCount = failedQuiz
                    });
                }
            }

            return weakness;
        }

        public async Task UpdateLessonProgress(
            Guid enrollmentId,
            Guid chapterId,
            Guid lessonId,
            double playedSeconds,
            double duration,
            bool isCompleted,
            Guid callerId)
        {
            await unitOfWork.BeginTransactionAsync();

            await unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .UpsertLessonProgress(enrollmentId, chapterId, lessonId, isCompleted);

            await unitOfWork.CommitAsync(callerId.ToString());
        }

        public async Task<(bool isCorrect, string explanation)> UpdateQuizProgress(
            Guid enrollmentId,
            Guid chapterId,
            Guid lessonId,
            Guid quizId,
            List<string> selectedAnswers,
            Guid callerId)
        {
            var enrollment = await unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .GetEnrollmentForUpdate(enrollmentId);

            if (enrollment == null)
                throw new NotFound("Enrollment not found");

            if (enrollment.StudentID != callerId)
                throw new AuthenticateException("You are not the owner of this enrollment");

            await unitOfWork.BeginTransactionAsync();
            var result = await unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .UpsertQuizProgress(enrollmentId, chapterId, lessonId, quizId, selectedAnswers);
            await unitOfWork.CommitAsync(callerId.ToString());

            return (result.isCorrect, result.explanation);
        }
        #endregion
    }
}


