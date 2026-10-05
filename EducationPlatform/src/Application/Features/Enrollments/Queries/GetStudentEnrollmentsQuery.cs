using Application.Results;
using MediatR;
using Domain.Common.Interfaces;
using AutoMapper;
using Application.Interface;
using Application.Exceptions;
using Domain.EnrollmentManagement.Aggregate;

namespace Application.Features.Enrollments.Queries
{
    public class GetStudentEnrollmentsQuery : IRequest<IEnumerable<EnrollmentDTO>>
    {
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
                .GetStudentEnrollments(_currentUser.Id.Value, request.PageIndex, request.PageSize, cancellationToken);

            return _mapper.Map<IEnumerable<EnrollmentDTO>>(list);
        }
    }
}
