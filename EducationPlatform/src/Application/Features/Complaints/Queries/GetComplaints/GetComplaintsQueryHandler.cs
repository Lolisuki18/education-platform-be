using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using MediatR;
using Application.Interface;

namespace Application.Features.Complaints.Queries.GetComplaints
{
    public class GetComplaintsQueryHandler : IRequestHandler<GetComplaintsQuery, IEnumerable<ComplaintDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public GetComplaintsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentUser = currentUser;
        }

        public async Task<IEnumerable<ComplaintDTO>> Handle(GetComplaintsQuery request, CancellationToken cancellationToken)
        {
            Guid? teacherId = null;
            if (string.Equals(_currentUser.Role, Role.Teacher.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                teacherId = _currentUser.Id;
            }

            var complaints = await _unitOfWork
                .GetRepository<IComplaintRepository>()
                .GetComplaintsAsync(request.Status, teacherId, request.PageIndex, request.PageSize, cancellationToken);

            return _mapper.Map<IEnumerable<ComplaintDTO>>(complaints);
        }
    }
}
