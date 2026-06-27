using Application.BusinessException;
using Application.Interface;
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
        private readonly ICurrentUser _currentUser;

        public GetComplaintDetailQueryHandler(IUnitOfWork unitOfWork, IMapper mapper, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentUser = currentUser;
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

            // Authorization check: Teacher can only view complaints of their own courses
            if (_currentUser.Role == Domain.IdentityManagement.ValueObject.Role.Teacher.ToString() &&
                complaint.Course.TeacherID != _currentUser.Id)
            {
                throw new ForbiddenException("You do not have permission to view this complaint.");
            }

            return _mapper.Map<ComplaintDetailDTO>(complaint);
        }
    }
}
