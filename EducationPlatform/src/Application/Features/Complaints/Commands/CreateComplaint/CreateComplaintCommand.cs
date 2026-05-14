using MediatR;

namespace Application.Features.Complaints.Commands.CreateComplaint
{
    public class CreateComplaintCommand : IRequest<Guid>
    {
        public Guid CourseID { get; set; }
        public string Reason { get; set; } = string.Empty;
        
        // Input from controller
        public Stream? EvidenceFileStream { get; set; }
        public string? EvidenceFileExtension { get; set; }

        // Used by handler internally if path is already known or calculated
        public string? EvidenceImagePath { get; set; }
    }
}
