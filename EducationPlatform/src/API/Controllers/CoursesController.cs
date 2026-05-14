using Application.Results;
using Application.Interface;
using Application.Features.Courses.Queries.GetPolicies;
using Application.Features.Complaints.Queries.GetComplaints;
using Application.Features.Complaints.Queries.GetComplaintDetail;
using Application.Features.Complaints.Commands.CreateComplaint;
using Application.Features.Complaints.Commands.ReviewComplaint;
using Application.Features.Academic.Queries.GetDefaultLessons;
using Application.Features.Courses.Queries.GetLandingPage;
using Application.Features.Courses.Queries.GetCourseDetail;
using API.Models.Courses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using API.Hubs;
using MediatR;

namespace API.Controllers
{
    [ApiController]
    [Route("api/courses")]
    public class CoursesController : ControllerBase
    {
        private readonly IStorageService storageService;
        private readonly ISpeechToTextService speechService;
        private readonly IHubContext<CourseHub> courseHub;
        private readonly IMediator mediator;
        private readonly AutoMapper.IMapper mapper;

        public CoursesController(
            IStorageService storageService,
            ISpeechToTextService speechService,
            IHubContext<CourseHub> courseHub,
            IMediator mediator,
            AutoMapper.IMapper mapper)
        {
            this.storageService = storageService;
            this.speechService = speechService;
            this.courseHub = courseHub;
            this.mediator = mediator;
            this.mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<ListCoursesResponseDto>> ListCourses([FromQuery] ListCoursesRequestDto request)
        {
            var query = mapper.Map<GetLandingPageQuery>(request);
            var result = await mediator.Send(query);

            return Ok(new ListCoursesResponseDto
            {
                Courses = result.Courses.ToList(),
                Grades = result.Grades.ToList(),
                Subjects = result.Subjects.ToList()
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<CourseDetailDTO>> GetCourseDetail(Guid id)
        {
            var course = await mediator.Send(new GetCourseDetailQuery { CourseID = id });
            return Ok(course);
        }

        [Authorize(Roles = "Teacher")]
        [HttpPost]
        public async Task<ActionResult<CreateCourseResponseDto>> CreateCourse([FromForm] CreateCourseRequestDto request)
        {
            // Note: In a pure Vertical Slice, the file upload might also be inside the handler.
            // But usually, we handle IFormFile in the Controller and pass the stream/path to the Command.
            
            string thumbnailName = await storageService.SaveAsync(
                request.Thumbnail.OpenReadStream(),
                Path.GetExtension(request.Thumbnail.FileName).TrimStart('.'),
                CancellationToken.None);

            var command = new Application.Features.Courses.CreateCourse.CreateCourseCommand
            {
                Title         = request.CreateCourse.Title,
                Description   = request.CreateCourse.Description,
                SubjectID     = request.CreateCourse.SubjectID,
                GradeID       = request.CreateCourse.GradeID,
                Price         = request.CreateCourse.Price,
                ThumbnailName = thumbnailName
            };

            var courseId = await mediator.Send(command);

            return Ok(new CreateCourseResponseDto
            {
                CourseID = courseId,
                Message  = "Course created successfully and is pending review."
            });
        }

        [Authorize(Roles = "Teacher")]
        [HttpPost("upload-chunk")]
        public async Task<IActionResult> UploadChunk(
            [FromForm] IFormFile chunk,
            [FromForm] string uploadId,
            [FromForm] int index,
            CancellationToken ct)
        {
            if (chunk == null || chunk.Length == 0)
                return BadRequest("Empty chunk");

            await using var stream = chunk.OpenReadStream();
            await storageService.SaveChunkAsync(stream, uploadId, index, ct);

            return Ok();
        }

        [Authorize(Roles = "Teacher")]
        [HttpPost("complete-upload")]
        public async Task<IActionResult> CompleteUpload(
            [FromForm] string uploadId,
            [FromForm] string extension,
            CancellationToken ct)
        {
            var path = await storageService.CompleteUploadAsync(uploadId, extension, ct);
            var fullPath = storageService.GetFullPath(path);

            // Fire and forget transcription
            _ = ExecuteTranscriptionAsync(fullPath);

            return Ok(new
            {
                url = path,
                transcriptUrl = (string?)null
            });
        }

        [HttpGet("default-lessons")]
        public async Task<IActionResult> GetDefaultLessons([FromQuery] Guid subjectId, [FromQuery] Guid gradeId)
        {
            var defaultLessons = await mediator.Send(new GetDefaultLessonsQuery { SubjectId = subjectId, GradeId = gradeId });
            return Ok(defaultLessons);
        }

        [Authorize(Roles = "Student")]
        [HttpPost("complaints")]
        public async Task<ActionResult<CreateComplaintResponseDto>> CreateComplaint([FromForm] CreateComplaintRequestDto request)
        {
            var command = new CreateComplaintCommand
            {
                CourseID               = request.CourseId,
                Reason                 = request.Reason,
                EvidenceFileStream     = request.EvidenceImage?.OpenReadStream(),
                EvidenceFileExtension  = request.EvidenceImage != null ? Path.GetExtension(request.EvidenceImage.FileName) : null
            };
            
            await mediator.Send(command);

            return Ok(new CreateComplaintResponseDto
            {
                Message = "Your complaint has been submitted and is waiting for admin review."
            });
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpGet("complaints")]
        public async Task<ActionResult<IEnumerable<ComplaintDTO>>> ListComplaints([FromQuery] Domain.CourseManagement.Enum.ComplaintStatus? status)
        {
            var complaints = await mediator.Send(new GetComplaintsQuery { Status = status });
            return Ok(complaints);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("complaints/{id:guid}")]
        public async Task<ActionResult<ReviewComplaintResponseDto>> GetComplaintDetailForReview(Guid id)
        {
            var complaint = await mediator.Send(new GetComplaintDetailQuery { ComplaintID = id });
            var course = await mediator.Send(new GetCourseDetailQuery { CourseID = complaint.CourseID });

            return Ok(new ReviewComplaintResponseDto
            {
                Complaint = complaint,
                Course    = course,
                Message   = "Complaint detail loaded."
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("complaints/review")]
        public async Task<ActionResult<ReviewComplaintResponseDto>> ReviewComplaint([FromBody] ReviewComplaintRequestDto request)
        {
            await mediator.Send(new ReviewComplaintCommand
            {
                ComplaintID = request.ComplaintID,
                IsApproved  = request.IsApproved,
                AdminNote   = request.AdminNote
            });

            var complaint = await mediator.Send(new GetComplaintDetailQuery { ComplaintID = request.ComplaintID });
            var course = await mediator.Send(new GetCourseDetailQuery { CourseID = complaint.CourseID });

            return Ok(new ReviewComplaintResponseDto
            {
                Message   = "Complaint reviewed successfully.",
                Complaint = complaint,
                Course    = course
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("review/{id:guid}")]
        public async Task<ActionResult<ReviewCourseResponseDto>> GetCourseForReview(Guid id)
        {
            var course = await mediator.Send(new GetCourseDetailQuery { CourseID = id });
            var policies = await mediator.Send(new GetPoliciesQuery());

            return Ok(new ReviewCourseResponseDto
            {
                Course   = course,
                Policies = policies,
                Message  = "Course loaded for review."
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("review")]
        public async Task<ActionResult<ReviewCourseResponseDto>> ReviewCourse([FromBody] ReviewCourseRequestDto request)
        {
            var command = mapper.Map<Application.Features.Courses.ReviewCourse.ReviewCourseCommand>(request);
            await mediator.Send(command);

            await courseHub.Clients.All.SendAsync("CourseReviewed", request.CourseID);

            var course = await mediator.Send(new GetCourseDetailQuery { CourseID = request.CourseID });
            var policies = await mediator.Send(new GetPoliciesQuery());

            return Ok(new ReviewCourseResponseDto
            {
                Message  = "Course reviewed successfully.",
                Course   = course,
                Policies = policies
            });
        }

        private async Task ExecuteTranscriptionAsync(string fullPath)
        {
            try
            {
                await speechService.TranscribeVideo(fullPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Transcription error: {ex.Message}");
            }
        }
    }
}
