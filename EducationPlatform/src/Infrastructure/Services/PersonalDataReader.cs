using Application.Interface;
using Application.Results;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services
{
    /// <summary>
    /// Reads every record of one user with plain projections (no tracking, no domain objects), so the download
    /// stays cheap and cannot leak a field that was not chosen on purpose: password hashes, one-time codes and
    /// refresh-session hashes are never part of it.
    /// </summary>
    public class PersonalDataReader : IPersonalDataReader
    {
        private readonly EducationPlatformDBContext _db;

        public PersonalDataReader(EducationPlatformDBContext db)
        {
            _db = db;
        }

        public async Task<PersonalDataExport?> ReadAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            var profile = await _db.Users
                .AsNoTracking()
                .Where(u => u.UserID == userId)
                .Select(u => new ExportedProfile
                {
                    UserID = u.UserID,
                    Email = u.Email,
                    Phone = u.Phone,
                    Name = u.Name,
                    Bio = u.Bio,
                    Role = u.Role.ToString(),
                    IsVerified = u.IsVerified,
                    CreatedAt = u.CreatedAt,
                    ActiveSessions = u.RefreshSessions.Count(s => s.RevokedAt == null && s.ExpiresAt > now)
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (profile == null)
                return null;

            var enrollments = await _db.Enrollments
                .AsNoTracking()
                .Where(e => e.StudentID == userId)
                .OrderByDescending(e => e.EnrolledAt)
                .Select(e => new ExportedEnrollment
                {
                    CourseID = e.CourseID,
                    CourseTitle = e.Course.Title,
                    Status = e.Status.ToString(),
                    EnrolledAt = e.EnrolledAt,
                    CompletedAt = e.CompletedAt
                })
                .ToListAsync(cancellationToken);

            var orders = await _db.Orders
                .AsNoTracking()
                .Where(o => o.StudentID == userId)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new ExportedOrder
                {
                    OrderCode = o.OrderCode,
                    CourseTitle = o.Course.Title,
                    TotalAmount = o.PlatformAmount + o.TeacherAmount,
                    Status = o.Status.ToString(),
                    CreatedAt = o.CreatedAt,
                    PaidAt = o.PaidAt
                })
                .ToListAsync(cancellationToken);

            var reviews = await _db.CourseReviews
                .AsNoTracking()
                .Where(r => r.StudentID == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new ExportedReview
                {
                    CourseTitle = r.Course.Title,
                    Rating = r.Rating,
                    Comment = r.Comment,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var complaints = await _db.Complaints
                .AsNoTracking()
                .Where(c => c.StudentID == userId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new ExportedComplaint
                {
                    CourseTitle = c.Course.Title,
                    Reason = c.Reason,
                    Status = c.Status.ToString(),
                    AdminNote = c.AdminNote,
                    CreatedAt = c.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var notifications = await _db.Notifications
                .AsNoTracking()
                .Where(n => n.UserID == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new ExportedNotification
                {
                    Title = n.Title,
                    Message = n.Message,
                    CreatedAt = n.CreatedAt,
                    ReadAt = n.ReadAt
                })
                .ToListAsync(cancellationToken);

            var coursesTaught = await _db.Courses
                .AsNoTracking()
                .Where(c => c.TeacherID == userId)
                .OrderBy(c => c.Title)
                .Select(c => new ExportedCourse
                {
                    CourseID = c.CourseID,
                    Title = c.Title,
                    Status = c.Status.ToString()
                })
                .ToListAsync(cancellationToken);

            return new PersonalDataExport
            {
                Profile = profile,
                Enrollments = enrollments,
                Orders = orders,
                Reviews = reviews,
                Complaints = complaints,
                Notifications = notifications,
                CoursesTaught = coursesTaught
            };
        }
    }
}
