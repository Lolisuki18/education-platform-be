using Application.BusinessException;
using Application.Features.Complaints.Commands.ReviewComplaint;
using Domain.CourseManagement.Aggregate;
using Domain.Common.Interfaces;
using MediatR;
using Application.Interface;

namespace Application.Features.Complaints.Commands.ReviewComplaint
{
    public class ReviewComplaintCommandHandler : IRequestHandler<ReviewComplaintCommand, Unit>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public ReviewComplaintCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<Unit> Handle(ReviewComplaintCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var courseRepo = _unitOfWork.GetRepository<ICourseRepository>();
            var complaint = await courseRepo.GetComplaintDetailByID(request.ComplaintID);

            if (complaint == null)
            {
                throw new NotFound($"Complaint with ID: {request.ComplaintID} is not found");
            }

            // 1. Domain logic: Update status and trigger event if approved
            if (request.IsApproved)
            {
                complaint.Approve(request.AdminNote);
            }
            else
            {
                complaint.Reject(request.AdminNote);
            }

            // 2. Persist
            await _unitOfWork.BeginTransactionAsync();
            courseRepo.UpdateComplaint(complaint);
            
            // Side effects (like rejecting the course) will be handled by ComplaintApprovedEventHandler
            // which is dispatched during CommitAsync via DomainEventDispatcherInterceptor.
            await _unitOfWork.CommitAsync(_currentUser.Id.Value.ToString());

            return Unit.Value;
        }
    }
}
