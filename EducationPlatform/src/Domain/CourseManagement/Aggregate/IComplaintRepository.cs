using Domain.CourseManagement.Entity;
using Domain.CourseManagement.Enum;
using Domain.Common.Interfaces;

namespace Domain.CourseManagement.Aggregate
{
    public interface IComplaintRepository : IGenericRepository<Complaint>
    {
        Task<Complaint?> GetComplaintDetailByID(Guid complaintId);

        Task<IEnumerable<Complaint>> GetComplaintsAsync(
            ComplaintStatus? complaintStatus,
            Guid? teacherId);

        Task<IEnumerable<Complaint>> GetApprovedByCoursesAsync(Guid courseId);

        void CreateComplaint(Complaint complaint);

        void UpdateComplaint(Complaint complaint);

        void RemoveComplaints(IEnumerable<Complaint> complaints);
    }
}
