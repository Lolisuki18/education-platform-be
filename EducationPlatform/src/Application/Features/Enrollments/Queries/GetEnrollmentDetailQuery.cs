using Application.Results;
using MediatR;
using Domain.Common.Interfaces;
using AutoMapper;
using Application.Exceptions;
using Application.Common;
using Application.Interface;
using Domain.EnrollmentManagement.Aggregate;

namespace Application.Features.Enrollments.Queries.GetEnrollmentDetail
{
    public class GetEnrollmentDetailQuery : IRequest<EnrollmentDetailDTO>
    {
        public Guid EnrollmentID { get; set; }
    }

    public class GetEnrollmentDetailQueryHandler : IRequestHandler<GetEnrollmentDetailQuery, EnrollmentDetailDTO>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;
        private readonly IMediaUrlSigner _mediaUrlSigner;

        public GetEnrollmentDetailQueryHandler(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ICurrentUser currentUser,
            IMediaUrlSigner mediaUrlSigner)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentUser = currentUser;
            _mediaUrlSigner = mediaUrlSigner;
        }

        public async Task<EnrollmentDetailDTO> Handle(GetEnrollmentDetailQuery request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var enrollment = await _unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .GetEnrollmentDetailByID(request.EnrollmentID, cancellationToken);

            if (enrollment == null)
                throw new NotFoundException("Enrollment detail not found");

            // Basic authorization check
            if (enrollment.StudentID != _currentUser.Id.Value && _currentUser.Role != "Admin")
            {
                throw new ForbiddenException("You do not have permission to view this enrollment.");
            }

            var dto = _mapper.Map<EnrollmentDetailDTO>(enrollment);

            HideQuizAnswers(dto);

            // The caller owns this enrollment (or is an admin), so they may watch: hand out expiring video links
            return dto.ProtectVideos(_mediaUrlSigner);
        }

        /// <summary>
        /// The student needs each question and its options, never the answers. That holds for the quizzes listed under the
        /// course as well as the ones under the progress: both would give the answers away. The explanation of a
        /// quiz usually does too, so it is only shown once the student has attempted that quiz.
        /// </summary>
        private static void HideQuizAnswers(EnrollmentDetailDTO dto)
        {
            var attempted = (dto.CourseProgress?.ChapterProgresses ?? new List<ChapterProgressDTO>())
                .SelectMany(c => c.LessonProgresses ?? new List<LessonProgressDTO>())
                .SelectMany(l => l.QuizProgresses ?? new List<QuizProgressDTO>())
                .Where(q => q.AttemptCount > 0)
                .Select(q => q.QuizID)
                .ToHashSet();

            void Hide(QuizDTO? quiz)
            {
                if (quiz == null)
                    return;

                if (quiz.Answer != null)
                    quiz.Answer.CorrectAnswers = new List<string>();

                if (!attempted.Contains(quiz.QuizID))
                    quiz.Note = null;
            }

            foreach (var quiz in (dto.Course?.Chapters ?? new List<ChapterDTO>())
                         .SelectMany(c => c.Lessons ?? new List<LessonDTO>())
                         .SelectMany(l => l.Quizzes ?? new List<QuizDTO>()))
            {
                Hide(quiz);
            }

            foreach (var quizProgress in (dto.CourseProgress?.ChapterProgresses ?? new List<ChapterProgressDTO>())
                         .SelectMany(c => c.LessonProgresses ?? new List<LessonProgressDTO>())
                         .SelectMany(l => l.QuizProgresses ?? new List<QuizProgressDTO>()))
            {
                Hide(quizProgress.Quiz);
            }
        }
    }
}
