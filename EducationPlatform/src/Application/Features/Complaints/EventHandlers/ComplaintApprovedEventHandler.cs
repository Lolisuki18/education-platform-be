using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Events;
using Domain.Common.Interfaces;
using Domain.EnrollmentManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using MediatR;

namespace Application.Features.Complaints.EventHandlers
{
    public class ComplaintApprovedEventHandler : INotificationHandler<ComplaintApprovedEvent>
    {
        private readonly IUnitOfWork _unitOfWork;

        public ComplaintApprovedEventHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task Handle(ComplaintApprovedEvent notification, CancellationToken cancellationToken)
        {
            var courseRepo = _unitOfWork.GetRepository<ICourseRepository>();
            var complaintRepo = _unitOfWork.GetRepository<IComplaintRepository>();

            // 1. Get already-approved complaints for this course
            var approvedComplaints = await complaintRepo.GetApprovedByCoursesAsync(notification.CourseId);
            var totalApproved = approvedComplaints.Count();

            var complaintCount = totalApproved + 1; // current + previous

            if (complaintCount >= 2)
            {
                var course = await courseRepo.GetByIdAsync(notification.CourseId, cancellationToken);
                if (course != null)
                {
                    // Fatality: Course removed due to multiple complaints
                    course.RejectByComplaint("Course removed due to multiple approved complaints.");
                    await courseRepo.UpdateAsync(course.CourseID, course, cancellationToken);

                    // Compensation & Penalties
                    var enrollmentRepo = _unitOfWork.GetRepository<IEnrollmentRepository>();
                    var orderRepo = _unitOfWork.GetRepository<IOrderRepository>();

                    var studentIds = await enrollmentRepo.GetEnrolledStudentIdsByCourseId(course.CourseID);

                    // 7.5% per student compensation
                    var couponAmount = course.Price.Amount * 0.075m;
                    var coupons = studentIds.Select(studentId =>
                        new Domain.OrderManagement.Aggregate.Coupon(
                            Guid.NewGuid(),
                            studentId,
                            $"CMP-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                            couponAmount,
                            "Compensation for removed course"
                        )).ToList();

                    if (coupons.Any())
                    {
                        orderRepo.CreateCoupons(coupons);

                        // Total penalty for teacher = total compensation sum
                        var totalPenaltyAmount = coupons.Sum(c => c.DiscountAmount);
                        var penalty = new Domain.OrderManagement.Aggregate.Penalty(
                            Guid.NewGuid(),
                            course.TeacherID,
                            course.CourseID,
                            totalPenaltyAmount,
                            $"Penalty equals total compensation for removed course for {studentIds.Count()} enrollments"
                        );

                        orderRepo.CreatePenalty(penalty);
                    }

                    var currentComplaint = await complaintRepo.GetComplaintDetailByID(notification.ComplaintId);
                    var allToRemove = approvedComplaints.ToList();
                    if (currentComplaint != null) allToRemove.Add(currentComplaint);

                    complaintRepo.RemoveComplaints(allToRemove);
                }
            }
            else
            {
                var course = await courseRepo.GetByIdAsync(notification.CourseId, cancellationToken);
                if (course != null)
                {
                    course.MarkAsRejected(DateTime.Now, "Rejected due to approved complaint.");
                    await courseRepo.UpdateAsync(course.CourseID, course, cancellationToken);
                }
            }
        }
    }
}
