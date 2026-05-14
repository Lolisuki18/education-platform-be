using Application.Results;
using Application.Interface;
using Application.Features.Courses.Queries.GetPolicies;
using Application.Features.Complaints.Queries.GetComplaints;
using Application.Features.Complaints.Queries.GetComplaintDetail;
using Application.Features.Complaints.Commands.CreateComplaint;
using Application.Features.Complaints.Commands.ReviewComplaint;
using Application.Features.Academic.Queries.GetGrades;
using Application.Features.Academic.Queries.GetSubjects;
using Application.Features.Academic.Queries.GetDefaultLessons;
using API.Helper;
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
            var getCoursesQuery = new Application.Features.Courses.Queries.GetCourses.GetCoursesQuery
            {
                Title = request.Title,
                GradeName = request.GradeName,
                SubjectName = request.SubjectName,
                PageIndex = request.PageIndex,
                PageSize = request.PageSize
            };

            if (User.Identity?.IsAuthenticated == true)
            {
                var (userId, role) = CheckClaimHelper.CheckClaim(User);
                getCoursesQuery.CallerId = userId;
                getCoursesQuery.CallerRole = role;
            }

            var courses = await mediator.Send(getCoursesQuery);

            var grades = await mediator.Send(new GetGradesQuery());
            var subjects = await mediator.Send(new GetSubjectsQuery());

            return Ok(new ListCoursesResponseDto
            {
                Courses = courses,
                Grades = grades,
                Subjects = subjects
            });
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<CourseDetailDTO>> GetCourseDetail(Guid id)
        {
            var query = new Application.Features.Courses.Queries.GetCourseDetail.GetCourseDetailQuery
            {
                CourseID = id
            };

            if (User.Identity?.IsAuthenticated == true)
            {
                var (userId, role) = CheckClaimHelper.CheckClaim(User);
                query.CallerId   = userId;
                query.CallerRole = role;
            }

            var course = await mediator.Send(query);
            return Ok(course);
        }

        [Authorize(Roles = "Teacher")]
        [HttpPost]
        public async Task<ActionResult<CreateCourseResponseDto>> CreateCourse([FromForm] CreateCourseRequestDto request)
        {
            string thumbnailName = await storageService.SaveAsync(
                request.Thumbnail.OpenReadStream(),
                Path.GetExtension(request.Thumbnail.FileName).TrimStart('.'),
                CancellationToken.None);

            var (userId, role) = CheckClaimHelper.CheckClaim(User);
            
            // Map the request to MediatR command
            var json = System.Text.Json.JsonSerializer.Serialize(request.CreateCourse);
            var command = System.Text.Json.JsonSerializer.Deserialize<Application.Features.Courses.CreateCourse.CreateCourseCommand>(json);
            
            command!.ThumbnailName = thumbnailName;
            command.CallerId = userId;
            command.CallerRole = role;

            // Send command via MediatR
            var courseId = await mediator.Send(command);
            
            await courseHub.Clients.All.SendAsync("CourseCreated", request.CreateCourse.Title);

            return Ok(new CreateCourseResponseDto
            {
                Message = "Course submitted for admin review.",
                Course = request.CreateCourse
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
            {
                return BadRequest("Empty chunk");
            }

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
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest("Reason is required.");
            }

            var (userId, _) = CheckClaimHelper.CheckClaim(User);

            string? imagePath = null;
            if (request.EvidenceImage != null && request.EvidenceImage.Length > 0)
            {
                imagePath = await storageService.SaveAsync(
                    request.EvidenceImage.OpenReadStream(),
                    Path.GetExtension(request.EvidenceImage.FileName).TrimStart('.'),
                    CancellationToken.None);
            }

            var command = new CreateComplaintCommand
            {
                CourseID          = request.CourseId,
                Reason            = request.Reason,
                EvidenceImagePath = imagePath,
                StudentId         = userId
            };
            
            await mediator.Send(command);

            return Ok(new CreateComplaintResponseDto
            {
                Message = "Your complaint has been submitted and is waiting for admin review."
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("complaints")]
        public async Task<ActionResult<IEnumerable<ComplaintDTO>>> ListComplaints([FromQuery] Domain.CourseManagement.Enum.ComplaintStatus? status)
        {
            var (userId, role) = CheckClaimHelper.CheckClaim(User);
            
            var query = new GetComplaintsQuery
            {
                Status = status
            };

            if (role == Domain.IdentityManagement.ValueObject.Role.Teacher.ToString())
            {
                query.TeacherId = userId;
            }

            var complaints = await mediator.Send(query);
            return Ok(complaints);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("complaints/{id:guid}")]
        public async Task<ActionResult<ReviewComplaintResponseDto>> GetComplaintDetail(Guid id)
        {
            var (userId, role) = CheckClaimHelper.CheckClaim(User);
            var complaint = await mediator.Send(new GetComplaintDetailQuery { ComplaintID = id });
            var course = await mediator.Send(
                new Application.Features.Courses.Queries.GetCourseDetail.GetCourseDetailQuery
                {
                    CourseID   = complaint.CourseID,
                    CallerId   = userId,
                    CallerRole = role
                });

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
            var (userId, _) = CheckClaimHelper.CheckClaim(User);
            
            var command = new ReviewComplaintCommand
            {
                ComplaintID = request.ComplaintID,
                IsApproved  = request.IsApproved,
                AdminNote   = request.AdminNote,
                CallerId    = userId
            };

            await mediator.Send(command);

            var complaint = await mediator.Send(new GetComplaintDetailQuery { ComplaintID = request.ComplaintID });
            var course = await mediator.Send(
                new Application.Features.Courses.Queries.GetCourseDetail.GetCourseDetailQuery
                {
                    CourseID   = complaint.CourseID,
                    CallerId   = userId,
                    CallerRole = "Admin"
                });

            return Ok(new ReviewComplaintResponseDto
            {
                Message   = "Complaint reviewed successfully.",
                Complaint = complaint,
                Course    = course
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("review/{id:guid}")]
        public async Task<ActionResult<ReviewCourseResponseDto>> GetReviewCourse(Guid id)
        {
            var (userId, role) = CheckClaimHelper.CheckClaim(User);
            var course = await mediator.Send(
                new Application.Features.Courses.Queries.GetCourseDetail.GetCourseDetailQuery
                {
                    CourseID   = id,
                    CallerId   = userId,
                    CallerRole = role
                });
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
            var (userId, _) = CheckClaimHelper.CheckClaim(User);

            // Build the MediatR command from API request DTO
            var command = new Application.Features.Courses.ReviewCourse.ReviewCourseCommand
            {
                CourseID        = request.CourseID,
                ViolatedPolicyIDs = request.ViolatedPolicyIDs,
                ViolatedChapters  = request.ViolatedChapters?
                    .Select(x => new Application.Features.Courses.ReviewCourse.ViolatedChapterItem
                    {
                        ViolatedChapterId = x.ViolatedChapterId,
                        AdminNote         = x.AdminNote
                    }).ToList(),
                AdminNote  = request.AdminNote,
                CallerId   = userId
            };

            await mediator.Send(command);

            await courseHub.Clients.All.SendAsync("CourseReviewed", request.CourseID);

            var course = await mediator.Send(
                new Application.Features.Courses.Queries.GetCourseDetail.GetCourseDetailQuery
                {
                    CourseID   = request.CourseID,
                    CallerId   = userId,
                    CallerRole = "Admin"
                });
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
                Console.WriteLine($"❌ Transcription error: {ex.Message}");
            }
        }
    }
}
