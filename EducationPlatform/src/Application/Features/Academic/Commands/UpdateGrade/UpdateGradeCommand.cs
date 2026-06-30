using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.BusinessException;
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
            var grade = await gradeRepo.GetByIdAsync(request.GradeID);

            if (grade == null)
                throw new NotFound("Grade not found.");

            var trimmedName = request.Name.Trim();
            var allGrades = await gradeRepo.GetAllAsync();
            if (allGrades.Any(g => g.GradeID != request.GradeID && string.Equals(g.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new Conflict($"Grade with name '{trimmedName}' already exists.");
            }

            grade.Update(trimmedName);

            if (request.IsActive)
            {
                grade.Activate();
            }
            else
            {
                if (await gradeRepo.IsInUse(request.GradeID))
                {
                    throw new Conflict("Cannot deactivate grade because it is currently in use.");
                }
                grade.Deactivate();
            }

            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());
        }
    }
}
