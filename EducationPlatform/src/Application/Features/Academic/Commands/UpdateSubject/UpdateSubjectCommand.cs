using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Exceptions;
using Application.Interface;
using Domain.AcademicManagement.Aggregate;
using Domain.Common.Interfaces;
using MediatR;

namespace Application.Features.Academic.Commands.UpdateSubject
{
    public class UpdateSubjectCommand : IRequest
    {
        public Guid SubjectID { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class UpdateSubjectCommandHandler : IRequestHandler<UpdateSubjectCommand>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public UpdateSubjectCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task Handle(UpdateSubjectCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var subjectRepo = _unitOfWork.GetRepository<ISubjectRepository>();
            var subject = await subjectRepo.GetByIdAsync(request.SubjectID, cancellationToken);

            if (subject == null)
                throw new NotFoundException("Subject not found.");

            var trimmedCode = request.Code.Trim();
            var trimmedName = request.Name.Trim();

            if (await subjectRepo.CodeExistsAsync(trimmedCode, request.SubjectID, cancellationToken))
            {
                throw new ConflictException($"Subject with code '{trimmedCode}' already exists.");
            }

            subject.Update(trimmedCode, trimmedName);

            if (request.IsActive)
            {
                subject.Activate();
            }
            else
            {
                if (await subjectRepo.IsInUse(request.SubjectID, cancellationToken))
                {
                    throw new ConflictException("Cannot deactivate subject because it is currently in use.");
                }
                subject.Deactivate();
            }

            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());
        }
    }
}
