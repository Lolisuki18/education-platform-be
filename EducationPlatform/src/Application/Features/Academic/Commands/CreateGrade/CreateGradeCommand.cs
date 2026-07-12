using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.BusinessException;
using Application.Interface;
using Domain.AcademicManagement.Aggregate;
using Domain.Common.Interfaces;
using MediatR;

namespace Application.Features.Academic.Commands.CreateGrade
{
    public class CreateGradeCommand : IRequest<Guid>
    {
        public string Name { get; set; } = string.Empty;
    }

    public class CreateGradeCommandHandler : IRequestHandler<CreateGradeCommand, Guid>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public CreateGradeCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<Guid> Handle(CreateGradeCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var gradeRepo = _unitOfWork.GetRepository<IGradeRepository>();
            var allGrades = await gradeRepo.GetAllAsync();

            var trimmedName = request.Name.Trim();
            if (allGrades.Any(g => string.Equals(g.Name, trimmedName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new Conflict($"Grade with name '{trimmedName}' already exists.");
            }

            var newGrade = new Grade(Guid.NewGuid(), trimmedName);
            gradeRepo.Add(newGrade);

            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());

            return newGrade.GradeID;
        }
    }
}
