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
            
            // 1. Get already-approved complaints for this course
            var approvedComplaints = await courseRepo.GetApprovedByCoursesAsync(notification.CourseId);
            var totalApproved = approvedComplaints.Count();
            
            var complaintCount = totalApproved + 1; // current + previous

            if (complaintCount >= 2)
            {
                var course = await courseRepo.GetByIdAsync(notification.CourseId);
                if (course != null)
                {
                    // Fatality: Course removed due to multiple complaints
                    course.RejectByComplaint("Course removed due to multiple approved complaints.");
                    courseRepo.Update(course.CourseID, course);

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

                    // Cleanup: Remove the complaints history as they are resolved by course removal
                    var currentComplaint = await courseRepo.GetComplaintDetailByID(notification.ComplaintId);
                    var allToRemove = approvedComplaints.ToList();
                    if (currentComplaint != null) allToRemove.Add(currentComplaint);
                    
                    courseRepo.RemoveComplaints(allToRemove);
                }
            }
            else
            {
                // Simple rejection if it's the first approved complaint
                var course = await courseRepo.GetByIdAsync(notification.CourseId);
                if (course != null)
                {
                    course.MarkAsRejected(DateTime.Now, "Rejected due to approved complaint.");
                    courseRepo.Update(course.CourseID, course);
                }
            }
        }
    }
}
