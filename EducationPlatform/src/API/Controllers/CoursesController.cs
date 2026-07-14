using Application.Results;
using Application.Interface;
using Application.Features.Courses.Queries.GetPolicies;
using Application.Features.Complaints.Queries.GetComplaints;
using Application.Features.Complaints.Queries.GetComplaintDetail;
using Application.Features.Complaints.Commands.CreateComplaint;
using Application.Features.Complaints.Commands.ReviewComplaint;
using Application.Features.Academic.Queries.GetDefaultLessons;
using Application.Features.Courses.Queries.GetLandingPage;
using Application.Features.Courses.Queries.GetCoursesPaged;
using Application.Features.Courses.Queries.GetCourseDetail;
using Application.Features.Courses.CreateCourse;
using API.Models.Courses;
using API.Models.Common;
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
        private readonly IHubContext<CourseHub> courseHub;
        private readonly IMediator mediator;
        private readonly AutoMapper.IMapper mapper;

        public CoursesController(
            IStorageService storageService,
            IHubContext<CourseHub> courseHub,
            IMediator mediator,
            AutoMapper.IMapper mapper)
        {
            this.storageService = storageService;
            this.courseHub = courseHub;
            this.mediator = mediator;
            this.mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResult<CourseDTO>>>> ListCourses([FromQuery] GetLandingPageQuery query, [FromQuery] string? status = null)
        {
            if (User.Identity?.IsAuthenticated == true && (User.IsInRole("Admin") || User.IsInRole("Teacher")))
            {
                var pagedQuery = new GetCoursesPagedQuery
                {
                    Title = query.Title,
                    Status = status,
                    PageIndex = query.PageIndex,
                    PageSize = query.PageSize
                };
                var pagedResult = await mediator.Send(pagedQuery);
                return Ok(ApiResponse<PagedResult<CourseDTO>>.Success(pagedResult));
            }

            var result = await mediator.Send(query);
            return Ok(ApiResponse<PagedResult<CourseDTO>>.Success(result));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResponse<CourseDetailDTO>>> GetCourseDetail(Guid id)
        {
            var course = await mediator.Send(new GetCourseDetailQuery { CourseID = id });
            return Ok(ApiResponse<CourseDetailDTO>.Success(course));
        }

        [Authorize(Roles = "Teacher")]
        [HttpPost]
        public async Task<ActionResult<ApiResponse<Guid>>> CreateCourse([FromForm] CreateCourseCommand command)
        {
            var courseId = await mediator.Send(command);

            return Ok(ApiResponse<Guid>.Success(courseId, "Course created successfully and is pending review."));
        }

        [Authorize(Roles = "Teacher")]
        [HttpPost("upload-chunk")]
        public async Task<ActionResult<ApiResponse>> UploadChunk(
            [FromForm] UploadChunkRequestDto request,
            CancellationToken ct)
        {
            if (request.Chunk == null || request.Chunk.Length == 0)
                return BadRequest(ApiResponse.Success("Empty chunk", 400));

            Application.Helper.FileValidator.Validate(request.Chunk);

            await using var stream = request.Chunk.OpenReadStream();
            await storageService.SaveChunkAsync(stream, request.UploadId, request.Index, ct);

            return Ok(ApiResponse.Success("Chunk uploaded successfully."));
        }

        [Authorize(Roles = "Teacher")]
        [HttpPost("complete-upload")]
        public async Task<ActionResult<ApiResponse<object>>> CompleteUpload(
            [FromForm] CompleteUploadRequestDto request,
            CancellationToken ct)
        {
            var path = await storageService.CompleteUploadAsync(request.UploadId, request.Extension, ct);
            var fullPath = storageService.GetFullPath(path);

            return Ok(ApiResponse<object>.Success(new
            {
                url = path,
                transcriptUrl = (string?)null
            }, "Upload completed successfully."));
        }

        [HttpGet("default-lessons")]
        public async Task<ActionResult<ApiResponse<IEnumerable<object>>>> GetDefaultLessons([FromQuery] Guid subjectId, [FromQuery] Guid gradeId)
        {
            var defaultLessons = await mediator.Send(new GetDefaultLessonsQuery { SubjectId = subjectId, GradeId = gradeId });
            return Ok(ApiResponse<IEnumerable<object>>.Success(defaultLessons));
        }

        [Authorize(Roles = "Student")]
        [HttpPost("complaints")]
        public async Task<ActionResult<ApiResponse<CreateComplaintResponseDto>>> CreateComplaint([FromForm] CreateComplaintRequestDto request)
        {
            var command = new CreateComplaintCommand
            {
                CourseID = request.CourseId,
                Reason = request.Reason,
                EvidenceFileStream = request.EvidenceImage?.OpenReadStream(),
                EvidenceFileExtension = request.EvidenceImage != null ? Path.GetExtension(request.EvidenceImage.FileName) : null
            };

            await mediator.Send(command);

            return Ok(ApiResponse<CreateComplaintResponseDto>.Success(new CreateComplaintResponseDto
            {
                Message = "Your complaint has been submitted and is waiting for admin review."
            }));
        }

        [Authorize(Roles = "Admin,Teacher")]
        [HttpGet("complaints")]
        public async Task<ActionResult<ApiResponse<IEnumerable<ComplaintDTO>>>> ListComplaints([FromQuery] Domain.CourseManagement.Enum.ComplaintStatus? status)
        {
            var complaints = await mediator.Send(new GetComplaintsQuery { Status = status });
            return Ok(ApiResponse<IEnumerable<ComplaintDTO>>.Success(complaints));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("complaints/{id:guid}")]
        public async Task<ActionResult<ApiResponse<ReviewComplaintResponseDto>>> GetComplaintDetailForReview(Guid id)
        {
            var complaint = await mediator.Send(new GetComplaintDetailQuery { ComplaintID = id });
            var course = await mediator.Send(new GetCourseDetailQuery { CourseID = complaint.CourseID });

            return Ok(ApiResponse<ReviewComplaintResponseDto>.Success(new ReviewComplaintResponseDto
            {
                Complaint = complaint,
                Course = course,
                Message = "Complaint detail loaded."
            }));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("complaints/review")]
        public async Task<ActionResult<ApiResponse<ReviewComplaintResponseDto>>> ReviewComplaint([FromBody] ReviewComplaintRequestDto request)
        {
            await mediator.Send(new ReviewComplaintCommand
            {
                ComplaintID = request.ComplaintID,
                IsApproved = request.IsApproved,
                AdminNote = request.AdminNote
            });

            var complaint = await mediator.Send(new GetComplaintDetailQuery { ComplaintID = request.ComplaintID });
            var course = await mediator.Send(new GetCourseDetailQuery { CourseID = complaint.CourseID });

            return Ok(ApiResponse<ReviewComplaintResponseDto>.Success(new ReviewComplaintResponseDto
            {
                Message = "Complaint reviewed successfully.",
                Complaint = complaint,
                Course = course
            }));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("review/{id:guid}")]
        public async Task<ActionResult<ApiResponse<ReviewCourseResponseDto>>> GetCourseForReview(Guid id)
        {
            var course = await mediator.Send(new GetCourseDetailQuery { CourseID = id });
            var policies = await mediator.Send(new GetPoliciesQuery());

            return Ok(ApiResponse<ReviewCourseResponseDto>.Success(new ReviewCourseResponseDto
            {
                Course = course,
                Policies = policies,
                Message = "Course loaded for review."
            }));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("review")]
        public async Task<ActionResult<ApiResponse<ReviewCourseResponseDto>>> ReviewCourse([FromBody] ReviewCourseRequestDto request)
        {
            var command = mapper.Map<Application.Features.Courses.ReviewCourse.ReviewCourseCommand>(request);
            await mediator.Send(command);

            await courseHub.Clients.All.SendAsync("CourseReviewed", request.CourseID);

            var course = await mediator.Send(new GetCourseDetailQuery { CourseID = request.CourseID });
            var policies = await mediator.Send(new GetPoliciesQuery());

            return Ok(ApiResponse<ReviewCourseResponseDto>.Success(new ReviewCourseResponseDto
            {
                Message = "Course reviewed successfully.",
                Course = course,
                Policies = policies
            }));
        }
    }
}
