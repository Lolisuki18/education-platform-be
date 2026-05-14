using Application.Interface;
using API.Models.AISupport;
using API.Helper;
using Application.Features.Courses.Queries.GetCourseDetail;
using Application.Features.Enrollments.Queries.GetStudentEnrollments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/ai-support")]
    [Authorize(Roles = "Student")]
    public class AISupportController : ControllerBase
    {
        private readonly IStorageService storageService;
        private readonly IAIService aiService;
        private readonly IMediator mediator;

        public AISupportController(
            IStorageService storageService,
            IAIService aiService,
            IMediator mediator)
        {
            this.storageService = storageService;
            this.aiService = aiService;
            this.mediator = mediator;
        }

        [HttpGet("load")]
        public async Task<ActionResult<AISupportLoadResponseDto>> Load([FromQuery] AISupportLoadRequestDto request)
        {
            var (userId, _) = CheckClaimHelper.CheckClaim(User);
            var enrollments = (await mediator.Send(new GetStudentEnrollmentsQuery())).ToList();

            var response = new AISupportLoadResponseDto
            {
                Enrollments = enrollments,
                SelectedEnrollmentId = request.SelectedEnrollmentId
            };

            if (request.SelectedEnrollmentId.HasValue)
            {
                var enrollment = enrollments.FirstOrDefault(e => e.EnrollmentID == request.SelectedEnrollmentId);
                if (enrollment != null)
                {
                    var courseDetail = await mediator.Send(new GetCourseDetailQuery
                    {
                        CourseID   = enrollment.CourseID
                    });
                    response.SelectedCourse = courseDetail;
                    response.Chapters = courseDetail.Chapters.OrderBy(c => c.Order).ToList();
                }
            }

            if (request.SelectedChapterId.HasValue && response.Chapters.Count > 0)
            {
                response.SelectedChapterId = request.SelectedChapterId;
                var chapter = response.Chapters.FirstOrDefault(c => c.ChapterID == request.SelectedChapterId);
                if (chapter != null)
                {
                    response.Lessons = chapter.Lessons.OrderBy(l => l.Order).ToList();
                }
            }

            if (request.SelectedLessonId.HasValue && response.Lessons.Count > 0)
            {
                response.SelectedLessonId = request.SelectedLessonId;
                response.SelectedLesson = response.Lessons.FirstOrDefault(l => l.LessonID == request.SelectedLessonId);
            }

            return Ok(response);
        }

        [HttpPost("generate-quiz")]
        public async Task<ActionResult<GenerateQuizResponseDto>> GenerateQuiz([FromBody] GenerateQuizRequestDto request)
        {
            var (userId, _) = CheckClaimHelper.CheckClaim(User);

            var enrollments = await mediator.Send(new GetStudentEnrollmentsQuery());
            var enrollment = enrollments.FirstOrDefault(e => e.EnrollmentID == request.EnrollmentId);

            if (enrollment == null)
            {
                return NotFound("Enrollment not found.");
            }

            var courseDetail = await mediator.Send(new GetCourseDetailQuery
            {
                CourseID   = enrollment.CourseID
            });
            var chapter = courseDetail.Chapters.FirstOrDefault(c => c.ChapterID == request.ChapterId);
            var lesson = chapter?.Lessons.FirstOrDefault(l => l.LessonID == request.LessonId);

            if (lesson == null)
            {
                return NotFound("Lesson not found.");
            }

            string transcript = await storageService.GetTranscriptFromVideoAsync(
                lesson.VideoUrl,
                default);

            var quiz = await aiService.GenerateQuizAsync(
                courseDetail.Grade.Name,
                courseDetail.Subject.Name,
                transcript);

            return Ok(new GenerateQuizResponseDto
            {
                GeneratedQuiz = quiz
            });
        }
    }
}
