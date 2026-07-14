using Application.Results;
using MediatR;
using Application.Interface;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.CourseManagement.Enum;
using Domain.IdentityManagement.Enum;

namespace Application.Features.Courses.Queries.GetCoursesPaged
{
    public class GetCoursesPagedQuery : IRequest<PagedResult<CourseDTO>>
    {
        public string? Title { get; set; }
        public string? Status { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class GetCoursesPagedQueryHandler : IRequestHandler<GetCoursesPagedQuery, PagedResult<CourseDTO>>
    {
        private readonly IApplicationDBContext _context;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public GetCoursesPagedQueryHandler(IApplicationDBContext context, IMapper mapper, ICurrentUser currentUser)
        {
            _context = context;
            _mapper = mapper;
            _currentUser = currentUser;
        }

        public async Task<PagedResult<CourseDTO>> Handle(GetCoursesPagedQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Courses
                .AsNoTracking()
                .Include(c => c.Teacher)
                .Include(c => c.Grade)
                .Include(c => c.Subject)
                .AsQueryable();

            // Filter by teacher if role is Teacher
            if (_currentUser.IsAuthenticated && _currentUser.Role == "Teacher")
            {
                query = query.Where(c => c.TeacherID == _currentUser.Id);
            }

            // Filter by Title
            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                query = query.Where(c => c.Title.Contains(request.Title));
            }

            // Filter by Status (with mapping for frontend status labels)
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                var statusStr = request.Status.Trim();
                if (statusStr.Equals("Pending", StringComparison.OrdinalIgnoreCase) || statusStr.Equals("InReview", StringComparison.OrdinalIgnoreCase))
                {
                    statusStr = "InReview";
                }
                else if (statusStr.Equals("Approved", StringComparison.OrdinalIgnoreCase) || statusStr.Equals("Published", StringComparison.OrdinalIgnoreCase))
                {
                    statusStr = "Published";
                }

                if (Enum.TryParse<CourseStatus>(statusStr, true, out var statusEnum))
                {
                    query = query.Where(c => c.Status == statusEnum);
                }
            }

            var totalItems = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((request.PageIndex - 1) * request.PageSize)
                .Take(request.PageSize)
                .ProjectTo<CourseDTO>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            return new PagedResult<CourseDTO>
            {
                Items = items.AsReadOnly(),
                PageIndex = request.PageIndex,
                PageSize = request.PageSize,
                TotalItems = totalItems
            };
        }
    }
}
