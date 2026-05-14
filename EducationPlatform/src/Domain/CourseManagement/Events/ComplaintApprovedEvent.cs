using Domain.Common;

namespace Domain.CourseManagement.Events
{
    public record ComplaintApprovedEvent(
        Guid ComplaintId,
        Guid CourseId) : IDomainEvent;
}
