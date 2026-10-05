using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Exceptions;
using Application.Interface;
using Domain.AcademicManagement.Aggregate;
using Domain.Common.Interfaces;
using MediatR;

namespace Application.Features.Academic.Commands.DeactivateSubject
{
    public class DeactivateSubjectCommand : IRequest
    {
        public Guid SubjectID { get; set; }
    }

    public class DeactivateSubjectCommandHandler : IRequestHandler<DeactivateSubjectCommand>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public DeactivateSubjectCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task Handle(DeactivateSubjectCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var subjectRepo = _unitOfWork.GetRepository<ISubjectRepository>();
            var subject = await subjectRepo.GetByIdAsync(request.SubjectID, cancellationToken);

            if (subject == null)
                throw new NotFoundException("Subject not found.");

            if (await subjectRepo.IsInUse(request.SubjectID, cancellationToken))
            {
                throw new ConflictException("Cannot deactivate subject because it is currently in use by courses or lessons.");
            }

            subject.Deactivate();

            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());
        }
    }
}
