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

        public string? EvidenceImagePath { get; set; }
    }
}
