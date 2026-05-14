using Application.Results;
using MediatR;
using Domain.Common.Interfaces;
using AutoMapper;
using Application.Interface;
using Application.BusinessException;

namespace Application.Features.Enrollments.Queries.GetStudentEnrollments
{
    public class GetStudentEnrollmentsQuery : IRequest<IEnumerable<EnrollmentDTO>>
    {
    }

    public class GetStudentEnrollmentsQueryHandler : IRequestHandler<GetStudentEnrollmentsQuery, IEnumerable<EnrollmentDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public GetStudentEnrollmentsQueryHandler(IUnitOfWork unitOfWork, IMapper mapper, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentUser = currentUser;
        }

        public async Task<IEnumerable<EnrollmentDTO>> Handle(GetStudentEnrollmentsQuery request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var list = await _unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .GetStudentEnrollments(_currentUser.Id.Value);

            return _mapper.Map<IEnumerable<EnrollmentDTO>>(list);
        }
    }
}
