using Application.BusinessException;
using Application.Features.Complaints.Commands.ReviewComplaint;
using Domain.CourseManagement.Aggregate;
using Infrastructure.Interface;
using MediatR;

namespace Application.Features.Complaints.Commands.ReviewComplaint
{
    public class ReviewComplaintCommandHandler : IRequestHandler<ReviewComplaintCommand, Unit>
    {
        private readonly IUnitOfWork _unitOfWork;

        public ReviewComplaintCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Unit> Handle(ReviewComplaintCommand request, CancellationToken cancellationToken)
        {
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
            await _unitOfWork.CommitAsync(request.CallerId.ToString());

            return Unit.Value;
        }
    }
}
