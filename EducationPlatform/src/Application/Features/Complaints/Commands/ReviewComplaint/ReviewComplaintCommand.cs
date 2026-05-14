using MediatR;

namespace Application.Features.Complaints.Commands.ReviewComplaint
{
    public class ReviewComplaintCommand : IRequest<Unit>
    {
        public Guid ComplaintID { get; set; }
        public bool IsApproved { get; set; }
        public string? AdminNote { get; set; }
        
        public Guid CallerId { get; set; }
    }
}
