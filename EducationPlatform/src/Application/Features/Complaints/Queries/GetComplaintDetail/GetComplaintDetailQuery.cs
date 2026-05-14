using Application.Results;
using MediatR;

namespace Application.Features.Complaints.Queries.GetComplaintDetail
{
    public class GetComplaintDetailQuery : IRequest<ComplaintDetailDTO>
    {
        public Guid ComplaintID { get; set; }
    }
}
