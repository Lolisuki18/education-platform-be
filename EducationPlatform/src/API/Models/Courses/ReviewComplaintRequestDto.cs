using System;

namespace API.Models.Courses
{
    public class ReviewComplaintRequestDto
    {
        public Guid ComplaintID { get; set; }
        public bool IsApproved { get; set; }
        public string? AdminNote { get; set; }

        // Backward compatibility if needed, but I'll update the controller
        public bool Approve => IsApproved;
    }
}
