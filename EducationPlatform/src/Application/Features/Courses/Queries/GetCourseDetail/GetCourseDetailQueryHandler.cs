using Application.BusinessException;
using Application.Results;
using AutoMapper;
using Domain.CourseManagement.Enum;
using Domain.IdentityManagement.ValueObject;
using Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Courses.Queries.GetCourseDetail
{
    /// <summary>
    /// Handler for GetCourseDetailQuery.
    ///
    /// Why NOT use ProjectTo here (unlike GetCourses):
    ///   CourseDetailDTO contains deeply nested collections
    ///   (Chapters → Lessons → Quizzes/Assignments/Materials) and Value Objects
    ///   (CoursePrice, QuizAnswer). AutoMapper's ProjectTo cannot translate
    ///   these into SQL at this nesting depth reliably.
    ///
    ///   Strategy used instead:
    ///     1. .AsNoTracking()  — still prevents Change Tracker overhead (read-only)
    ///     2. .AsSplitQuery()  — avoids cartesian explosion across multiple Includes
    ///     3. .Include() chain — same as the legacy repository, but owned by the slice
    ///     4. mapper.Map<CourseDetailDTO>() — applied in-memory after DB fetch
    ///
    /// Visibility rules:
    ///   - Unauthenticated / null role → Published only, no Chapters (public preview)
    ///   - Student                     → Published only, no Chapters
    ///   - Teacher (own course only)   → full detail
    ///   - Admin                       → full detail (any status)
    /// </summary>
    public class GetCourseDetailQueryHandler : IRequestHandler<GetCourseDetailQuery, CourseDetailDTO>
    {
        private readonly EducationPlatformDBContext _dbContext;
        private readonly IMapper _mapper;

        public GetCourseDetailQueryHandler(
            EducationPlatformDBContext dbContext,
            IMapper mapper)
        {
            _dbContext = dbContext;
            _mapper = mapper;
        }

        public async Task<CourseDetailDTO> Handle(
            GetCourseDetailQuery request,
            CancellationToken cancellationToken)
        {
            // ---------- 1. Parse caller role ----------
            Role? role = null;
            if (!string.IsNullOrWhiteSpace(request.CallerRole))
            {
                if (!Enum.TryParse<Role>(request.CallerRole, true, out var parsed))
                    throw new AuthenticateException("Invalid role");
                role = parsed;
            }

            // ---------- 2. Fetch with full Include chain (.AsNoTracking — read-only) ----------
            var course = await _dbContext.Courses
                .AsNoTracking()
                .AsSplitQuery()
                .Include(c => c.Teacher)
                .Include(c => c.Grade)
                .Include(c => c.Subject)
                .Include(c => c.ViolatedPolicies)
                    .ThenInclude(vp => vp.Policy)
                        .ThenInclude(p => p.PolicyRules)
                .Include(c => c.Chapters)
                    .ThenInclude(ch => ch.Lessons)
                        .ThenInclude(l => l.Quizzes)
                .Include(c => c.Chapters)
                    .ThenInclude(ch => ch.Lessons)
                        .ThenInclude(l => l.Assignments)
                .Include(c => c.Chapters)
                    .ThenInclude(ch => ch.Lessons)
                        .ThenInclude(l => l.Materials)
                .FirstOrDefaultAsync(c => c.CourseID == request.CourseID, cancellationToken);

            if (course == null)
                throw new NotFound($"Course with ID: {request.CourseID} is not found");

            // ---------- 3. Visibility guard: public / student → must be Published ----------
            bool isAdmin   = role == Role.Admin;
            bool isTeacher = role == Role.Teacher;

            if (!isAdmin && !isTeacher)
            {
                if (course.Status != CourseStatus.Published)
                    throw new NotFound($"Course with ID: {request.CourseID} is not found");
            }

            // ---------- 4. Map to DTO ----------
            var dto = _mapper.Map<CourseDetailDTO>(course);

            // ---------- 5. Apply visibility rules on DTO ----------
            // Students and anonymous users cannot see course content (only metadata)
            if (role == Role.Student || role == null)
            {
                dto.Chapters = new List<ChapterDTO>();
            }

            // Teachers can only see their own course's content
            if (isTeacher && request.CallerId.HasValue && course.TeacherID != request.CallerId.Value)
            {
                dto.Chapters = new List<ChapterDTO>();
            }

            return dto;
        }
    }
}
