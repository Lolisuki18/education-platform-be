using Application.Results;
using Application.BusinessException;
using AutoMapper;
using Domain.CourseManagement.Enum;
using Domain.IdentityManagement.ValueObject;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using MediatR;
using Application.Interface;

namespace Application.Features.Courses.Queries.GetCourses
{
    public class GetCoursesQueryHandler : IRequestHandler<GetCoursesQuery, List<CourseDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public GetCoursesQueryHandler(IUnitOfWork unitOfWork, IMapper mapper, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentUser = currentUser;
        }

        public async Task<List<CourseDTO>> Handle(GetCoursesQuery request, CancellationToken cancellationToken)
        {
            // Role parsing if caller is authenticated
            Role? role = null;
            if (_currentUser.IsAuthenticated && !string.IsNullOrWhiteSpace(_currentUser.Role))
            {
                if (!Enum.TryParse<Role>(_currentUser.Role, true, out var parsedRole))
                {
                    throw new AuthenticateException("Invalid role");
                }
                role = parsedRole;
            }

            Guid? teacherId = role == Role.Teacher ? _currentUser.Id : null;

            var courses = await _unitOfWork
                .GetRepository<ICourseRepository>()
                .GetAllCourses(
                    request.Title,
                    request.Price,
                    request.TeacherName,
                    request.GradeName,
                    request.SubjectName,
                    request.PageIndex,
                    request.PageSize,
                    teacherId,
                    role);

            var result = _mapper.Map<List<CourseDTO>>(courses);

            if (result == null || !result.Any())
            {
                throw new NotFound("Course list is not found or empty");
            }

            return result;
        }
    }
}
