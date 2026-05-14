using MediatR;

namespace Application.Features.Complaints.Commands.CreateComplaint
{
    public class CreateComplaintCommand : IRequest<Guid>
    {
        public Guid CourseID { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? EvidenceImagePath { get; set; }
        
        public Guid StudentId { get; set; }
    }
}
