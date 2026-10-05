using Application.Exceptions;
using Application.Features.Complaints.Commands.CreateComplaint;
using Domain.CourseManagement.Aggregate;
using Domain.Common.Interfaces;
using Domain.EnrollmentManagement.Aggregate;
using MediatR;
using Application.Interface;

namespace Application.Features.Complaints.Commands.CreateComplaint
{
    public class CreateComplaintCommandHandler : IRequestHandler<CreateComplaintCommand, Guid>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IStorageService _storageService;

        public CreateComplaintCommandHandler(
            IUnitOfWork unitOfWork,
            ICurrentUser currentUser,
            IStorageService storageService)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _storageService = storageService;
        }

        public async Task<Guid> Handle(CreateComplaintCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            Guid studentId = _currentUser.Id.Value;

            // 1. Validate enrollment
            var enrollmentRepo = _unitOfWork.GetRepository<IEnrollmentRepository>();
            var enrollments = await enrollmentRepo.GetStudentEnrollments(studentId, cancellationToken: cancellationToken);

            var isEnrolled = enrollments.Any(e => e.CourseID == request.CourseID);
            if (!isEnrolled)
            {
                throw new ConflictException("You can only submit complaints for courses you have enrolled in.");
            }

            // 2. Handle image upload if provided
            string? imagePath = request.EvidenceImagePath;
            if (request.EvidenceFileStream != null && !string.IsNullOrWhiteSpace(request.EvidenceFileExtension))
            {
                var allowedImageExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var ext = "." + request.EvidenceFileExtension.TrimStart('.').ToLowerInvariant();
                if (!allowedImageExtensions.Contains(ext))
                    throw new BadRequestException("Evidence file must be an image (.jpg, .jpeg, .png).");

                var length = request.EvidenceFileStream.CanSeek ? request.EvidenceFileStream.Length : 0;
                Application.Helpers.FileValidator.Validate(request.EvidenceFileStream, length, "evidence" + ext);

                imagePath = await _storageService.SaveAsync(
                    request.EvidenceFileStream,
                    request.EvidenceFileExtension.TrimStart('.'),
                    cancellationToken);
            }

            // 3. Create Domain Entity
            var complaint = new Complaint(
                Guid.NewGuid(),
                request.CourseID,
                studentId,
                request.Reason,
                imagePath);

            // 4. Persist
            try
            {
                await _unitOfWork.BeginTransactionAsync();

                var complaintRepo = _unitOfWork.GetRepository<IComplaintRepository>();
                complaintRepo.CreateComplaint(complaint);

                await _unitOfWork.CommitAsync(studentId.ToString());
            }
            catch
            {
                if (request.EvidenceFileStream != null && imagePath != null)
                {
                    await _storageService.DeleteAsync(imagePath);
                }
                throw;
            }

            return complaint.ComplaintID;
        }
    }
}
