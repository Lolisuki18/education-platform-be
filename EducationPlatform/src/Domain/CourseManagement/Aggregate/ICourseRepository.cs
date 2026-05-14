using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using Domain.CourseManagement.Enum;
using Domain.IdentityManagement.ValueObject;

namespace Domain.CourseManagement.Aggregate
{
    public interface ICourseRepository
    {
        Task<Course?> GetByIdAsync(Guid id);
        Task<IEnumerable<Course>> GetAllAsync();
        void Add(Course entity);
        Course Update(Guid id, Course entity);
        void Remove(Guid id);
        Task<IEnumerable<Course>> GetAllCourses(
            string? title,
            decimal? price,
            string? teacherName,
            string? gradeName,
            string? subjectName,
            int pageIndex,
            int pageSize,
            Guid? teacherId,
            Role? callerRole);

        Task<Course?> GetCourseDetailByID(
            Guid courseId);

        Task<Complaint?> GetComplaintDetailByID(
            Guid complaintId);

        Task<IEnumerable<Complaint>> GetComplaintsAsync(
            ComplaintStatus? complaintStatus,
            Guid? teacherId);

        Task<IEnumerable<Complaint>> GetApprovedByCoursesAsync(
            Guid courseId);

        void ReplaceViolatedPolicies(
            Guid courseId,
            IEnumerable<ViolatedPolicy> newViolatedPolicies);

        void AddChapters(
            IEnumerable<Chapter> chapters);

        void AddLessons(
            IEnumerable<Lesson> lessons);

        void AddQuizzes(
            IEnumerable<Quiz> quizzes);

        void AddAssignments(
            IEnumerable<Assignment> assignments);

        void AddMaterials(
            IEnumerable<Material> materials);

        void CreateComplaint(
            Complaint complaint);

        void UpdateComplaint(
            Complaint complaint);

        void RemoveComplaints(
            IEnumerable<Complaint> complaints);

        Task<(
            int InReview,
            int Rejected,
            int Published,
            Dictionary<string, int> GradeCounts,
            Dictionary<string, int> SubjectCounts
        )> Summary(
            DateTime? from,
            DateTime? to);

        Task<Dictionary<string, List<(string Label, decimal Value)>>> AnalyticsGrowth(
            DateTime? from,
            DateTime? to,
            string groupBy,
            Guid? gradeId,
            Guid? subjectId);
    }
}

