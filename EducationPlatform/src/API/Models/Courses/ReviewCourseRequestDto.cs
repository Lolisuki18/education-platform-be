using System;
using System.Collections.Generic;

namespace API.Models.Courses
{
    public class ReviewCourseRequestDto
    {
        public Guid CourseID { get; set; }
        public List<Guid>? ViolatedPolicyIDs { get; set; }
        public List<ViolatedChapterRequestItem>? ViolatedChapters { get; set; }
        public string? AdminNote { get; set; }
    }

    public class ViolatedChapterRequestItem
    {
        public Guid ViolatedChapterId { get; set; }
        public string? AdminNote { get; set; }
    }
}
