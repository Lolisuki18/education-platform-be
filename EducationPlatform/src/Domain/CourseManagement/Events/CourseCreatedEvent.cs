using Domain.Common;
using System;

namespace Domain.CourseManagement.Events
{
    public class CourseCreatedEvent : IDomainEvent
    {
        public Guid CourseId { get; }
        public string Title { get; }

        public CourseCreatedEvent(Guid courseId, string title)
        {
            CourseId = courseId;
            Title = title;
        }
    }
}
