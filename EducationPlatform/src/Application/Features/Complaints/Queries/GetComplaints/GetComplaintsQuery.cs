using Application.Results;
using Domain.CourseManagement.Enum;
using MediatR;

namespace Application.Features.Complaints.Queries.GetComplaints
{
    public class GetComplaintsQuery : IRequest<IEnumerable<ComplaintDTO>>
    {
        public ComplaintStatus? Status { get; set; }
        private int _pageIndex = 1;
        public int PageIndex
        {
            get => _pageIndex;
            set => _pageIndex = Application.Common.Paging.NormalizePageIndex(value);
        }
        private int _pageSize = Application.Common.Paging.DefaultPageSize;
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = Application.Common.Paging.NormalizePageSize(value);
        }
    }
}
