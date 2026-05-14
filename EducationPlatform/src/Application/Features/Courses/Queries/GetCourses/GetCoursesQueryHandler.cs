using Application.Results;
using Application.BusinessException;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Domain.CourseManagement.Enum;
using Domain.IdentityManagement.ValueObject;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Courses.Queries.GetCourses
{
    public class GetCoursesQueryHandler : IRequestHandler<GetCoursesQuery, List<CourseDTO>>
    {
        private readonly EducationPlatformDBContext _dbContext;
        private readonly IMapper _mapper;

        public GetCoursesQueryHandler(EducationPlatformDBContext dbContext, IMapper mapper)
        {
            _dbContext = dbContext;
            _mapper = mapper;
        }

        public async Task<List<CourseDTO>> Handle(GetCoursesQuery request, CancellationToken cancellationToken)
        {
            // Safety guards for paging
            int pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
            int pageSize = request.PageSize <= 0 ? 10 : request.PageSize;

            var query = _dbContext.Courses.AsNoTracking();

            // Role parsing if caller is authenticated
            Role? role = null;
            if (!string.IsNullOrWhiteSpace(request.CallerRole))
            {
                if (!Enum.TryParse<Role>(request.CallerRole, true, out var parsedRole))
                {
                    throw new AuthenticateException("Invalid role");
                }
                role = parsedRole;
            }

            Guid? teacherId = role == Role.Teacher ? request.CallerId : null;

            // ---------- Filters ----------
            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                query = query.Where(c => EF.Functions.Like(c.Title, $"%{request.Title}%"));
            }

            if (request.Price.HasValue)
            {
                query = query.Where(c => c.Price.Amount == request.Price.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.TeacherName))
            {
                query = query.Where(c => EF.Functions.Like(c.Teacher.Name, $"%{request.TeacherName}%"));
            }

            if (!string.IsNullOrWhiteSpace(request.GradeName))
            {
                query = query.Where(c => EF.Functions.Like(c.Grade.Name, $"%{request.GradeName}%"));
            }

            if (!string.IsNullOrWhiteSpace(request.SubjectName))
            {
                query = query.Where(c => EF.Functions.Like(c.Subject.Name, $"%{request.SubjectName}%"));
            }

            if (teacherId.HasValue)
            {
                query = query.Where(c => c.TeacherID == teacherId.Value);
            }

            if (role == Role.Student || role == null)
            {
                query = query.Where(c => c.Status == CourseStatus.Published);
            }

            // ---------- Sorting and Paging ----------
            query = query.OrderByDescending(c => c.CreatedAt)
                         .Skip((pageIndex - 1) * pageSize)
                         .Take(pageSize);

            // Execute via ProjectTo to optimize SQL Select
            var result = await query
                .ProjectTo<CourseDTO>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            if (result == null || !result.Any())
            {
                throw new NotFound("Course list is not found or empty");
            }

            return result;
        }
    }
}
