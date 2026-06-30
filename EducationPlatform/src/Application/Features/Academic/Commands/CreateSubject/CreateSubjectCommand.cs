using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.BusinessException;
using Application.Interface;
using Domain.AcademicManagement.Aggregate;
using Domain.Common.Interfaces;
using MediatR;

namespace Application.Features.Academic.Commands.CreateSubject
{
    public class CreateSubjectCommand : IRequest<Guid>
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class CreateSubjectCommandHandler : IRequestHandler<CreateSubjectCommand, Guid>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public CreateSubjectCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<Guid> Handle(CreateSubjectCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var subjectRepo = _unitOfWork.GetRepository<ISubjectRepository>();
            var allSubjects = await subjectRepo.GetAllAsync();

            var trimmedCode = request.Code.Trim();
            var trimmedName = request.Name.Trim();

            if (allSubjects.Any(s => string.Equals(s.Code, trimmedCode, StringComparison.OrdinalIgnoreCase)))
            {
                throw new Conflict($"Subject with code '{trimmedCode}' already exists.");
            }

            var newSubject = new Subject(Guid.NewGuid(), trimmedCode, trimmedName, Guid.Empty);
            subjectRepo.Add(newSubject);

            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());

            return newSubject.SubjectID;
        }
    }
}
