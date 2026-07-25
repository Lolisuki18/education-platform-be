using System;

namespace API.Models.Courses
{
    public class ReviewComplaintRequestDto
    {
        public Guid ComplaintID { get; set; }
        public bool IsApproved { get; set; }
        public string? AdminNote { get; set; }

        public bool Approve => IsApproved;
    }
}
