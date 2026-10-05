using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Exceptions;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using MediatR;

namespace Application.Features.Users.Queries
{
    public class GetUsersQuery : IRequest<PagedResult<UserDTO>>
    {
        public Role? Role { get; set; }
        public int PageIndex { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PagedResult<UserDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public GetUsersQueryHandler(IUnitOfWork unitOfWork, IMapper mapper, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentUser = currentUser;
        }

        public async Task<PagedResult<UserDTO>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var (users, totalCount) = await _unitOfWork
                .GetRepository<IUserRepository>()
                .GetUsersPaged(request.PageIndex, request.PageSize, request.Role, cancellationToken);

            var userDtos = _mapper.Map<IEnumerable<UserDTO>>(users).ToList();

            return new PagedResult<UserDTO>
            {
                Items = userDtos.AsReadOnly(),
                PageIndex = request.PageIndex,
                PageSize = request.PageSize,
                TotalItems = totalCount
            };
        }
    }
}
