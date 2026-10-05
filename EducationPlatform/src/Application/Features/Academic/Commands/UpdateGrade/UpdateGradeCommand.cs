using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Exceptions;
using Application.Interface;
using Domain.AcademicManagement.Aggregate;
using Domain.Common.Interfaces;
using MediatR;

namespace Application.Features.Academic.Commands.UpdateGrade
{
    public class UpdateGradeCommand : IRequest
    {
        public Guid GradeID { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class UpdateGradeCommandHandler : IRequestHandler<UpdateGradeCommand>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public UpdateGradeCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task Handle(UpdateGradeCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var gradeRepo = _unitOfWork.GetRepository<IGradeRepository>();
            var grade = await gradeRepo.GetByIdAsync(request.GradeID, cancellationToken);

            if (grade == null)
                throw new NotFoundException("Grade not found.");

            var trimmedName = request.Name.Trim();
            if (await gradeRepo.NameExistsAsync(trimmedName, request.GradeID, cancellationToken))
            {
                throw new ConflictException($"Grade with name '{trimmedName}' already exists.");
            }

            grade.Update(trimmedName);

            if (request.IsActive)
            {
                grade.Activate();
            }
            else
            {
                if (await gradeRepo.IsInUse(request.GradeID, cancellationToken))
                {
                    throw new ConflictException("Cannot deactivate grade because it is currently in use.");
                }
                grade.Deactivate();
            }

            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());
        }
    }
}
