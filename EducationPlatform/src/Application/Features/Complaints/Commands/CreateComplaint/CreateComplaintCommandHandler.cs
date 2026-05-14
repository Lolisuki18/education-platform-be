using Application.BusinessException;
using Application.Features.Complaints.Commands.CreateComplaint;
using Domain.CourseManagement.Aggregate;
using Infrastructure.Interface;
using MediatR;

namespace Application.Features.Complaints.Commands.CreateComplaint
{
    public class CreateComplaintCommandHandler : IRequestHandler<CreateComplaintCommand, Guid>
    {
        private readonly IUnitOfWork _unitOfWork;

        public CreateComplaintCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Guid> Handle(CreateComplaintCommand request, CancellationToken cancellationToken)
        {
            // 1. Validate enrollment
            var enrollmentRepo = _unitOfWork.GetRepository<IEnrollmentRepository>();
            var enrollments = await enrollmentRepo.GetStudentEnrollments(request.StudentId);

            var isEnrolled = enrollments.Any(e => e.CourseID == request.CourseID);
            if (!isEnrolled)
            {
                throw new Conflict("You can only submit complaints for courses you have enrolled in.");
            }

            // 2. Create Domain Entity
            var complaint = new Complaint(
                Guid.NewGuid(),
                request.CourseID,
                request.StudentId,
                request.Reason,
                request.EvidenceImagePath);

            // 3. Persist
            await _unitOfWork.BeginTransactionAsync();
            
            var courseRepo = _unitOfWork.GetRepository<ICourseRepository>();
            courseRepo.CreateComplaint(complaint);

            await _unitOfWork.CommitAsync(request.StudentId.ToString());

            return complaint.ComplaintID;
        }
    }
}
