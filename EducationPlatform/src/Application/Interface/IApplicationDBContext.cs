using Domain.CourseManagement.Aggregate;
using Domain.AcademicManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.EnrollmentManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using Microsoft.EntityFrameworkCore;

namespace Application.Interface
{
    public interface IApplicationDBContext
    {
        DbSet<Course> Courses { get; }
        DbSet<Grade> Grades { get; }
        DbSet<Subject> Subjects { get; }
        DbSet<User> Users { get; }
        DbSet<Enrollment> Enrollments { get; }
        DbSet<Order> Orders { get; }
        
        // Add other sets as needed for queries
        
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}
