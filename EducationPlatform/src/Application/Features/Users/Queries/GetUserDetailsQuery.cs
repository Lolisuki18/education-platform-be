using Application.Exceptions;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.EnrollmentManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using MediatR;


namespace Application.Features.Users.Queries
{
    public class GetUserDetailsQuery : IRequest<UserDTO>
    {
        public Guid UserId { get; set; }
    }

    public class GetUserDetailsHandler : IRequestHandler<GetUserDetailsQuery, UserDTO>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public GetUserDetailsHandler(IUnitOfWork unitOfWork, IMapper mapper, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentUser = currentUser;
        }
        public async Task<UserDTO> Handle(GetUserDetailsQuery request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var userId = request.UserId == Guid.Empty ? _currentUser.Id.Value : request.UserId;
            var user = await _unitOfWork.GetRepository<IUserRepository>().GetByIdAsync(userId);

            if (user == null)
                throw new NotFoundException("User detail not found");

            var dto = _mapper.Map<UserDTO>(user);

            return dto;

        }
    }
}
