using Application.BusinessException;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Users.Commands
{
    public class UpdateUserDetailsCommand : IRequest<UserDTO>
    {
        public string Name { get; set; }

        public string Phone { get; set; }

        public string Bio { get; set; }
    }

    public class UpdateUserDetailsCommandHandler : IRequestHandler<UpdateUserDetailsCommand, UserDTO>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ICurrentUser _currentUser;

        public UpdateUserDetailsCommandHandler(IUnitOfWork unitOfWork, IMapper mapper, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _currentUser = currentUser;
        }
        public async Task<UserDTO> Handle(UpdateUserDetailsCommand request, CancellationToken cancellationToken)
        {
            //check users'authorize
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var userId = _currentUser.Id.Value;

            var user = await _unitOfWork.GetRepository<IUserRepository>().GetByIdAsync(userId);

            if (user == null)
                throw new NotFound($"User with ID: {userId} not found.");

            user.UpdateProfile(request.Name, request.Phone, request.Bio);

            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());

            var dto = _mapper.Map<UserDTO>(user);
            return dto;
        }
    }
}
