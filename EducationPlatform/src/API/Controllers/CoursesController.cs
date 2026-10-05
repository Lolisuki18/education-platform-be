using Asp.Versioning;
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
using Microsoft.AspNetCore.RateLimiting;
using Application.Options;
using API.Extensions;
using Microsoft.AspNetCore.SignalR;
using API.Hubs;
using MediatR;

namespace API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/courses")]
    [Route("api/v{version:apiVersion}/courses")]
    public class CoursesController : ControllerBase
    {
        private readonly IStorageService storageService;
        private readonly IHubContext<CourseHub> courseHub;
        private readonly IMediator mediator;
        private readonly AutoMapper.IMapper mapper;
        private readonly ICurrentUser currentUser;

        public CoursesController(
            IStorageService storageService,
            IHubContext<CourseHub> courseHub,
            IMediator mediator,
            AutoMapper.IMapper mapper,
            ICurrentUser currentUser)
        {
            this.storageService = storageService;
            this.courseHub = courseHub;
            this.mediator = mediator;
            this.mapper = mapper;
            this.currentUser = currentUser;
        }

        [AllowAnonymous]
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

        [AllowAnonymous]
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResponse<CourseDetailDTO>>> GetCourseDetail(Guid id)
        {
            var course = await mediator.Send(new GetCourseDetailQuery { CourseID = id });
            return Ok(ApiResponse<CourseDetailDTO>.Success(course));
        }

        [Authorize(Roles = "Teacher")]
        [HttpPost]
        public async Task<ActionResult<ApiResponse<Guid>>> CreateCourse([FromForm] CreateCourseDto request)
        {
            var command = new CreateCourseCommand
            {
                Title = request.Title,
                Description = request.Description,
                Price = request.Price,
                ThumbnailName = request.ThumbnailName,
                ThumbnailFileStream = request.ThumbnailFile?.OpenReadStream(),
                ThumbnailFileExtension = request.ThumbnailFile != null ? Path.GetExtension(request.ThumbnailFile.FileName) : null,
                Slug = request.Slug,
                Prerequisites = request.Prerequisites,
                LearningOutcomes = request.LearningOutcomes,
                GradeID = request.GradeID,
                SubjectID = request.SubjectID,
                Chapters = request.Chapters.Select(c => new CreateChapterCommandDto
                {
                    Title = c.Title,
                    Description = c.Description,
                    Order = c.Order,
                    Lessons = c.Lessons.Select(l => new CreateLessonCommandDto
                    {
                        Title = l.Title,
                        Objectives = l.Objectives,
                        Description = l.Description,
                        VideoUrl = l.VideoUrl,
                        Order = l.Order,
                        Quizzes = l.Quizzes.Select(q => new CreateQuizCommandDto
                        {
                            Question = q.Question,
                            Note = q.Note,
                            Answer = new CreateQuizAnswerCommandDto
                            {
                                Type = q.Answer.Type,
                                CorrectAnswers = q.Answer.CorrectAnswers,
                                Options = q.Answer.Options,
                                TrueOrFalse = q.Answer.TrueOrFalse
                            }
                        }).ToList(),
                        Assignments = l.Assignments.Select(a => new CreateAssignmentCommandDto
                        {
                            Title = a.Title,
                            Description = a.Description,
                            MaxScore = a.MaxScore
                        }).ToList(),
                        Materials = l.Materials.Select(m => new CreateMaterialCommandDto
                        {
                            Name = m.Name,
                            Description = m.Description,
                            Url = m.Url,
                            Type = m.Type
                        }).ToList()
                    }).ToList()
                }).ToList()
            };

            var courseId = await mediator.Send(command);

            return Ok(ApiResponse<Guid>.Success(courseId, "Course created successfully and is pending review."));
        }

        [Authorize(Roles = "Teacher")]
        [EnableRateLimiting(RateLimitPolicies.Upload)]
        [RequestSizeLimit(UploadOptions.RequestSizeLimitBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = UploadOptions.RequestSizeLimitBytes)]
        [HttpPost("upload-chunk")]
        public async Task<ActionResult<ApiResponse>> UploadChunk(
            [FromForm] UploadChunkRequestDto request,
            CancellationToken ct)
        {
            if (request.Chunk == null || request.Chunk.Length == 0)
                return BadRequest(ApiResponse.Error("Empty chunk"));

            await using var stream = request.Chunk.OpenReadStream();
            Application.Helpers.FileValidator.Validate(stream, request.Chunk.Length, request.Chunk.FileName);
            await storageService.SaveChunkAsync(stream, request.UploadId, request.Index, currentUser.Id!.Value, ct);

            return Ok(ApiResponse.Success("Chunk uploaded successfully."));
        }

        [Authorize(Roles = "Teacher")]
        [HttpPost("complete-upload")]
        public async Task<ActionResult<ApiResponse<object>>> CompleteUpload(
            [FromForm] CompleteUploadRequestDto request,
            CancellationToken ct)
        {
            var path = await storageService.CompleteUploadAsync(request.UploadId, request.Extension, currentUser.Id!.Value, ct);
            var fullPath = storageService.GetFullPath(path);

            return Ok(ApiResponse<object>.Success(new
            {
                url = path,
                transcriptUrl = (string?)null
            }, "Upload completed successfully."));
        }

        [AllowAnonymous]
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
        public async Task<ActionResult<ApiResponse<IEnumerable<ComplaintDTO>>>> ListComplaints(
            [FromQuery] Domain.CourseManagement.Enum.ComplaintStatus? status,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10)
        {
            if (pageSize > 100) pageSize = 100;
            if (pageSize <= 0) pageSize = 10;
            if (pageIndex <= 0) pageIndex = 1;

            var complaints = await mediator.Send(new GetComplaintsQuery
            {
                Status = status,
                PageIndex = pageIndex,
                PageSize = pageSize
            });
            return Ok(ApiResponse<IEnumerable<ComplaintDTO>>.Success(complaints));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("complaints/{id:guid}")]
        public async Task<ActionResult<ApiResponse<ReviewComplaintResponseDto>>> GetComplaintDetailForReview(Guid id)
        {
            var response = await BuildComplaintReviewResponse(id, "Complaint detail loaded.");
            return Ok(ApiResponse<ReviewComplaintResponseDto>.Success(response));
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

            var response = await BuildComplaintReviewResponse(request.ComplaintID, "Complaint reviewed successfully.");
            return Ok(ApiResponse<ReviewComplaintResponseDto>.Success(response));
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("review/{id:guid}")]
        public async Task<ActionResult<ApiResponse<ReviewCourseResponseDto>>> GetCourseForReview(Guid id)
        {
            var response = await BuildCourseReviewResponse(id, "Course loaded for review.");
            return Ok(ApiResponse<ReviewCourseResponseDto>.Success(response));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("review")]
        public async Task<ActionResult<ApiResponse<ReviewCourseResponseDto>>> ReviewCourse([FromBody] ReviewCourseRequestDto request)
        {
            var command = mapper.Map<Application.Features.Courses.ReviewCourse.ReviewCourseCommand>(request);
            await mediator.Send(command);

            await courseHub.Clients.All.SendAsync("CourseReviewed", request.CourseID);

            var response = await BuildCourseReviewResponse(request.CourseID, "Course reviewed successfully.");
            return Ok(ApiResponse<ReviewCourseResponseDto>.Success(response));
        }

        private async Task<ReviewComplaintResponseDto> BuildComplaintReviewResponse(Guid complaintId, string message)
        {
            var complaint = await mediator.Send(new GetComplaintDetailQuery { ComplaintID = complaintId });
            var course = await mediator.Send(new GetCourseDetailQuery { CourseID = complaint.CourseID });

            return new ReviewComplaintResponseDto
            {
                Complaint = complaint,
                Course = course,
                Message = message
            };
        }

        private async Task<ReviewCourseResponseDto> BuildCourseReviewResponse(Guid courseId, string message)
        {
            var course = await mediator.Send(new GetCourseDetailQuery { CourseID = courseId });
            var policies = await mediator.Send(new GetPoliciesQuery());

            return new ReviewCourseResponseDto
            {
                Course = course,
                Policies = policies,
                Message = message
            };
        }
    }
}
