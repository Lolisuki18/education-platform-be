using Application.Results;
using Application.Interface;
using API.Helper;
using API.Models.Enrollments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api/enrollments")]
    [Authorize(Roles = "Student")]
    public class EnrollmentsController : ControllerBase
    {
        private readonly IEnrollmentService enrollmentService;

        public EnrollmentsController(IEnrollmentService enrollmentService)
        {
            this.enrollmentService = enrollmentService;
        }

        [HttpGet]
        public async Task<ActionResult<ListEnrollmentsResponseDto>> ListEnrollments()
        {
            var (userId, _) = CheckClaimHelper.CheckClaim(User);
            var enrollments = await enrollmentService.GetStudentEnrollments(userId);
            return Ok(new ListEnrollmentsResponseDto
            {
                Enrollments = enrollments
            });
        }

        [HttpGet("{enrollmentId:guid}")]
        public async Task<ActionResult<ResumeEnrollmentResponseDto>> GetEnrollment(Guid enrollmentId)
        {
            var enrollment = await enrollmentService.GetEnrollmentDetail(enrollmentId);
            return Ok(new ResumeEnrollmentResponseDto
            {
                Enrollment = enrollment
            });
        }

        [HttpPost("progress/lesson")]
        public async Task<IActionResult> UpdateLessonProgress([FromBody] UpdateLessonProgressRequestDto request)
        {
            var (userId, _) = CheckClaimHelper.CheckClaim(User);

            await enrollmentService.UpdateLessonProgress(
                request.EnrollmentId,
                request.ChapterId,
                request.LessonId,
                request.PlayedSeconds,
                request.Duration,
                request.IsCompleted,
                userId);

            return Ok();
        }

        [HttpPost("progress/quiz")]
        public async Task<ActionResult<UpdateQuizProgressResponseDto>> UpdateQuizProgress([FromBody] UpdateQuizProgressRequestDto request)
        {
            var (userId, _) = CheckClaimHelper.CheckClaim(User);

            var result = await enrollmentService.UpdateQuizProgress(
                request.EnrollmentId,
                request.ChapterId,
                request.LessonId,
                request.QuizId,
                request.SelectedAnswers,
                userId);

            return Ok(new UpdateQuizProgressResponseDto
            {
                IsCorrect = result.isCorrect,
                Explanation = result.explanation
            });
        }
    }
}
