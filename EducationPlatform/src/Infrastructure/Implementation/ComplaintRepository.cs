using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.CourseManagement.Enum;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Implementation
{
    public class ComplaintRepository : GenericRepository<Complaint>, IComplaintRepository
    {
        public ComplaintRepository(EducationPlatformDBContext context) : base(context) { }

        public async Task<Complaint?> GetComplaintDetailByID(Guid complaintId, CancellationToken cancellationToken = default)
        {
            return await context.Complaints
                .AsNoTracking()
                .Include(c => c.User) // Student
                .Include(c => c.Course)
                    .ThenInclude(c => c.Teacher) // Teacher
                .FirstOrDefaultAsync(c => c.ComplaintID == complaintId, cancellationToken);
        }

        public async Task<IEnumerable<Complaint>> GetComplaintsAsync(
            ComplaintStatus? complaintStatus,
            Guid? teacherId,
            int pageIndex = 1,
            int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            var query = context.Complaints
                .AsNoTracking()
                .Include(c => c.User) // Student
                .Include(c => c.Course)
                    .ThenInclude(c => c.Teacher)
                .AsQueryable();

            // Filter by status (if provided)
            if (complaintStatus.HasValue)
            {
                query = query.Where(c => c.Status == complaintStatus.Value);
            }

            // Filter by teacher (if provided)
            if (teacherId.HasValue)
            {
                query = query.Where(c => c.Course.TeacherID == teacherId.Value);
            }

            return await query
                .OrderByDescending(c => c.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Complaint>> GetApprovedByCoursesAsync(Guid courseId, CancellationToken cancellationToken = default)
        {
            // RemoveComplaints() re-attaches results explicitly, so no-tracking is safe here too.
            return await context.Complaints
                .AsNoTracking()
                .Where(c => c.CourseID == courseId &&
                            c.Status == ComplaintStatus.Approved)
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> HasPendingComplaintAsync(Guid studentId, Guid courseId, CancellationToken cancellationToken = default)
        {
            return await context.Complaints
                .AsNoTracking()
                .AnyAsync(c => c.StudentID == studentId &&
                               c.CourseID == courseId &&
                               c.Status == ComplaintStatus.Pending, cancellationToken);
        }

        public void CreateComplaint(Complaint complaint)
        {
            if (complaint == null)
                return;

            context.Complaints.Add(complaint);
        }

        public void UpdateComplaint(Complaint complaint)
        {
            context.Complaints.Update(complaint);
        }

        public void RemoveComplaints(IEnumerable<Complaint> complaints)
        {
            foreach (var complaint in complaints.ToList())
            {
                // The complaint being reviewed is already tracked, and a second copy of it (the handler reads it again)
                // cannot be attached: remove the tracked one. Copies read without tracking are attached first.
                var tracked = context.ChangeTracker.Entries<Complaint>()
                    .Select(e => e.Entity)
                    .FirstOrDefault(c => c.ComplaintID == complaint.ComplaintID);

                if (tracked != null)
                {
                    context.Complaints.Remove(tracked);
                    continue;
                }

                context.Complaints.Attach(complaint);
                context.Complaints.Remove(complaint);
            }
        }
    }
}
