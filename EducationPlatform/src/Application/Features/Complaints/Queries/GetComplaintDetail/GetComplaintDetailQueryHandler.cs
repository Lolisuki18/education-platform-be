using Application.BusinessException;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using MediatR;

namespace Application.Features.Complaints.Queries.GetComplaintDetail
{
    public class GetComplaintDetailQueryHandler : IRequestHandler<GetComplaintDetailQuery, ComplaintDetailDTO>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GetComplaintDetailQueryHandler(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<ComplaintDetailDTO> Handle(GetComplaintDetailQuery request, CancellationToken cancellationToken)
        {
            var complaint = await _unitOfWork
                .GetRepository<ICourseRepository>()
                .GetComplaintDetailByID(request.ComplaintID);

            if (complaint == null)
            {
                throw new NotFound($"Complaint with ID: {request.ComplaintID} is not found");
            }

            return _mapper.Map<ComplaintDetailDTO>(complaint);
        }
    }
}
