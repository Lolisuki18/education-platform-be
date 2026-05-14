using Application.BusinessException;
using Application.Features.Complaints.Commands.CreateComplaint;
using Domain.CourseManagement.Aggregate;
using Domain.Common.Interfaces;
using MediatR;
using Application.Interface;

namespace Application.Features.Complaints.Commands.CreateComplaint
{
    public class CreateComplaintCommandHandler : IRequestHandler<CreateComplaintCommand, Guid>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public CreateComplaintCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<Guid> Handle(CreateComplaintCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            Guid studentId = _currentUser.Id.Value;

            // 1. Validate enrollment
            var enrollmentRepo = _unitOfWork.GetRepository<IEnrollmentRepository>();
            var enrollments = await enrollmentRepo.GetStudentEnrollments(studentId);

            var isEnrolled = enrollments.Any(e => e.CourseID == request.CourseID);
            if (!isEnrolled)
            {
                throw new Conflict("You can only submit complaints for courses you have enrolled in.");
            }

            // 2. Create Domain Entity
            var complaint = new Complaint(
                Guid.NewGuid(),
                request.CourseID,
                studentId,
                request.Reason,
                request.EvidenceImagePath);

            // 3. Persist
            await _unitOfWork.BeginTransactionAsync();
            
            var courseRepo = _unitOfWork.GetRepository<ICourseRepository>();
            courseRepo.CreateComplaint(complaint);

            await _unitOfWork.CommitAsync(studentId.ToString());

            return complaint.ComplaintID;
        }
    }
}
