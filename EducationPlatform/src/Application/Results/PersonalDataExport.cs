namespace Application.Results
{
    /// <summary>Everything the platform holds about one person (the "right of access" download).</summary>
    public class PersonalDataExport
    {
        public DateTime ExportedAt { get; set; }
        public ExportedProfile Profile { get; set; } = new();
        public List<ExportedEnrollment> Enrollments { get; set; } = new();
        public List<ExportedOrder> Orders { get; set; } = new();
        public List<ExportedReview> Reviews { get; set; } = new();
        public List<ExportedComplaint> Complaints { get; set; } = new();
        public List<ExportedNotification> Notifications { get; set; } = new();

        /// <summary>Courses the person teaches (empty for students).</summary>
        public List<ExportedCourse> CoursesTaught { get; set; } = new();
    }

    public class ExportedProfile
    {
        public Guid UserID { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ActiveSessions { get; set; }
    }

    public class ExportedEnrollment
    {
        public Guid CourseID { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime EnrolledAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class ExportedOrder
    {
        public long OrderCode { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
    }

    public class ExportedReview
    {
        public string CourseTitle { get; set; } = string.Empty;
        public float Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ExportedComplaint
    {
        public string CourseTitle { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? AdminNote { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ExportedNotification
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ReadAt { get; set; }
    }

    public class ExportedCourse
    {
        public Guid CourseID { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
