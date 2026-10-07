using Application.Exceptions;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.IdentityManagement.Aggregate;
using MediatR;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Features.Users.Commands
{
    public class UpdateUserDetailsCommand : IRequest<UserDTO>
    {
        public string? Name { get; set; }

        public string? Phone { get; set; }

        public string? Bio { get; set; }
    }

    public class UpdateUserDetailsCommandValidator : FluentValidation.AbstractValidator<UpdateUserDetailsCommand>
    {
        public UpdateUserDetailsCommandValidator()
        {
            // Blank fields mean "leave as is", so each rule only applies to a value that was sent
            RuleFor(x => x.Name)
                .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

            RuleFor(x => x.Phone)
                .Matches(@"^\d{10,11}$").WithMessage("Phone must be 10-11 digits.")
                .When(x => !string.IsNullOrWhiteSpace(x.Phone));

            RuleFor(x => x.Bio)
                .MaximumLength(1000).WithMessage("Bio must not exceed 1000 characters.");
        }
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

            var user = await _unitOfWork.GetRepository<IUserRepository>().GetByIdAsync(userId, cancellationToken);

            if (user == null)
                throw new NotFoundException($"User with ID: {userId} not found.");

            // Phone numbers are unique across accounts (registration enforces it too)
            if (!string.IsNullOrWhiteSpace(request.Phone))
            {
                var owner = await _unitOfWork.GetRepository<IUserRepository>().GetUserByPhone(request.Phone, cancellationToken);
                if (owner != null && owner.UserID != userId)
                    throw new ConflictException($"User with phone {request.Phone} already exists.");
            }

            user.UpdateProfile(request.Name, request.Phone, request.Bio);

            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());

            var dto = _mapper.Map<UserDTO>(user);
            return dto;
        }
    }
}
