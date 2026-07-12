using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.BusinessException;
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
            var subject = await subjectRepo.GetByIdAsync(request.SubjectID);

            if (subject == null)
                throw new NotFound("Subject not found.");

            var trimmedCode = request.Code.Trim();
            var trimmedName = request.Name.Trim();

            var allSubjects = await subjectRepo.GetAllAsync();
            if (allSubjects.Any(s => s.SubjectID != request.SubjectID && string.Equals(s.Code, trimmedCode, StringComparison.OrdinalIgnoreCase)))
            {
                throw new Conflict($"Subject with code '{trimmedCode}' already exists.");
            }

            subject.Update(trimmedCode, trimmedName);

            if (request.IsActive)
            {
                subject.Activate();
            }
            else
            {
                if (await subjectRepo.IsInUse(request.SubjectID))
                {
                    throw new Conflict("Cannot deactivate subject because it is currently in use.");
                }
                subject.Deactivate();
            }

            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());
        }
    }
}
