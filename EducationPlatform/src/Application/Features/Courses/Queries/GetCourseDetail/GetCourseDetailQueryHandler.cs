using Application.BusinessException;
using Application.Results;
using AutoMapper;
using Domain.CourseManagement.Enum;
using Domain.IdentityManagement.ValueObject;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using MediatR;
using Application.Interface;

namespace Application.Features.Courses.Queries.GetCourseDetail
{
    public class GetCourseDetailQueryHandler : IRequestHandler<GetCourseDetailQuery, CourseDetailDTO>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public GetCourseDetailQueryHandler(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentUser = currentUser;
        }

        public async Task<CourseDetailDTO> Handle(
            GetCourseDetailQuery request,
            CancellationToken cancellationToken)
        {
            // ---------- 1. Parse caller role ----------
            Role? role = null;
            if (_currentUser.IsAuthenticated && !string.IsNullOrWhiteSpace(_currentUser.Role))
            {
                if (!Enum.TryParse<Role>(_currentUser.Role, true, out var parsed))
                    throw new AuthenticateException("Invalid role");
                role = parsed;
            }

            // ---------- 2. Fetch Metadata first ----------
            var courseMetadata = await _unitOfWork
                .GetRepository<ICourseRepository>()
                .GetCourseMetadataByID(request.CourseID);

            if (courseMetadata == null)
                throw new NotFound($"Course with ID: {request.CourseID} is not found");

            // ---------- 3. Visibility guard: public / student → must be Published ----------
            bool isAdmin = role == Role.Admin;
            bool isTeacher = role == Role.Teacher;

            if (!isAdmin && !isTeacher)
            {
                if (courseMetadata.Status != CourseStatus.Published)
                    throw new NotFound($"Course with ID: {request.CourseID} is not found");
            }

            // ---------- 4. Determine if detailed chapters are needed ----------
            // Only Admin or the Teacher who owns the course is allowed to see the chapters/lessons content
            bool canViewChapters = isAdmin || (isTeacher && _currentUser.Id.HasValue && courseMetadata.TeacherID == _currentUser.Id.Value);

            Course? course = null;
            if (canViewChapters)
            {
                course = await _unitOfWork
                    .GetRepository<ICourseRepository>()
                    .GetCourseDetailByID(request.CourseID);
            }
            else
            {
                course = courseMetadata;
            }

            if (course == null)
                throw new NotFound($"Course with ID: {request.CourseID} is not found");

            // ---------- 5. Map to DTO ----------
            var dto = _mapper.Map<CourseDetailDTO>(course);

            // ---------- 6. Apply visibility rules on DTO ----------
            // Students and anonymous users cannot see course content (only metadata)
            if (role == Role.Student || role == null)
            {
                dto.Chapters = new List<ChapterDTO>();
            }

            // Teachers can only see their own course's content
            if (isTeacher && _currentUser.Id.HasValue && course.TeacherID != _currentUser.Id.Value)
            {
                dto.Chapters = new List<ChapterDTO>();
            }

            return dto;
        }
    }
}
