using Application.Results;
using Domain.CourseManagement.Enum;
using MediatR;

namespace Application.Features.Complaints.Queries.GetComplaints
{
    public class GetComplaintsQuery : IRequest<IEnumerable<ComplaintDTO>>
    {
        public ComplaintStatus? Status { get; set; }
        public Guid? TeacherId { get; set; }
    }
}
