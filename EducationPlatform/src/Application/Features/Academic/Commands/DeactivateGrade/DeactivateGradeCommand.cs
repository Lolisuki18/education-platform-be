using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Exceptions;
using Application.Interface;
using Domain.AcademicManagement.Aggregate;
using Domain.Common.Interfaces;
using MediatR;

namespace Application.Features.Academic.Commands.DeactivateGrade
{
    public class DeactivateGradeCommand : IRequest
    {
        public Guid GradeID { get; set; }
    }

    public class DeactivateGradeCommandHandler : IRequestHandler<DeactivateGradeCommand>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public DeactivateGradeCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task Handle(DeactivateGradeCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var gradeRepo = _unitOfWork.GetRepository<IGradeRepository>();
            var grade = await gradeRepo.GetByIdAsync(request.GradeID);

            if (grade == null)
                throw new NotFoundException("Grade not found.");

            if (await gradeRepo.IsInUse(request.GradeID))
            {
                throw new ConflictException("Cannot deactivate grade because it is currently in use by courses or lessons.");
            }

            grade.Deactivate();

            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());
        }
    }
}
