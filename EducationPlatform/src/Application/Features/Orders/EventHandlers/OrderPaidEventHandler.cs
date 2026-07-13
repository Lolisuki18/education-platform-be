using Domain.OrderManagement.Events;
using MediatR;
using Domain.Common.Interfaces;
using Domain.EnrollmentManagement.Aggregate;
using Application.Interface;
using Domain.IdentityManagement.Aggregate;
using Domain.CourseManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using Microsoft.Extensions.Configuration;

namespace Application.Features.Orders.EventHandlers
{
    public class OrderPaidEventHandler : INotificationHandler<OrderPaidEvent>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public OrderPaidEventHandler(IUnitOfWork unitOfWork, IEmailService emailService, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _configuration = configuration;
        }

        public async Task Handle(OrderPaidEvent notification, CancellationToken cancellationToken)
        {
            // Create enrollment when order is paid
            var enrollment = new Enrollment(
                Guid.NewGuid(),
                notification.StudentID,
                notification.CourseID,
                null);

            _unitOfWork
                .GetRepository<IEnrollmentRepository>()
                .Add(enrollment);

            // Send payment confirmation email
            try
            {
                var student = await _unitOfWork.GetRepository<IUserRepository>().GetByIdAsync(notification.StudentID);
                var course = await _unitOfWork.GetRepository<ICourseRepository>().GetByIdAsync(notification.CourseID);
                var order = await _unitOfWork.GetRepository<IOrderRepository>().GetByIdAsync(notification.OrderID);

                if (student != null && course != null && order != null)
                {
                    var frontendUrl = _configuration["PayOS:FrontendUrl"] ?? "http://localhost:3000";
                    string subject = "Payment Confirmation - " + course.Title;
                    string amountStr = (course.Price?.Amount ?? 0).ToString("N0") + " VND";
                    string body = BuildPaymentSuccessEmailBody(student.Name, course.Title, order.OrderCode.ToString(), amountStr, frontendUrl);

                    // Fire-and-forget email sending so it does not block the HTTP redirect response
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.SendEmailAsync(student.Email, subject, body);
                        }
                        catch (Exception)
                        {
                            // Ignored in background task
                        }
                    });
                }
            }
            catch (Exception)
            {
                // Prevent email sending failures from reverting order completion
            }

            await Task.CompletedTask;
        }

        private static string BuildPaymentSuccessEmailBody(string studentName, string courseTitle, string orderCode, string amount, string frontendUrl)
        {
            return $@"
            <!DOCTYPE html>
            <html>
            <body style='font-family:Segoe UI, Tahoma, Geneva, Verdana, sans-serif;background:#f9f9f9;padding:40px'>
                <div style='max-width:600px;margin:auto;background:#fff;
                            padding:30px;border-radius:10px;border:1px solid #e1e2ed;box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05)'>
                    <div style='text-align:center;padding-bottom:20px;border-bottom:1px solid #f0f0f0'>
                        <h2 style='color:#004ac6;margin:0'>Payment Successful!</h2>
                        <p style='color:#737686;margin:5px 0 0 0'>Thank you for your purchase</p>
                    </div>
                    <div style='padding:20px 0;line-height:1.6;color:#434655'>
                        <p>Hi <strong>{studentName}</strong>,</p>
                        <p>Your payment for the following course has been successfully processed. You now have full access to your learning materials.</p>
                        
                        <div style='background:#faf8ff;padding:15px;border-radius:8px;margin:20px 0;border:1px solid #e1e2ed'>
                            <table style='width:100%;font-size:14px'>
                                <tr>
                                    <td style='color:#737686;padding:5px 0'>Course:</td>
                                    <td style='font-weight:bold;text-align:right;color:#191b23'>{courseTitle}</td>
                                </tr>
                                <tr>
                                    <td style='color:#737686;padding:5px 0'>Order Code:</td>
                                    <td style='font-weight:bold;text-align:right;color:#191b23'>#{orderCode}</td>
                                </tr>
                                <tr>
                                    <td style='color:#737686;padding:5px 0'>Amount Paid:</td>
                                    <td style='font-weight:bold;text-align:right;color:#004ac6;font-size:16px'>{amount}</td>
                                </tr>
                            </table>
                        </div>
                        
                        <p>To start learning, please visit your <a href='{frontendUrl}/student' style='color:#004ac6;text-decoration:none;font-weight:bold'>Student Workspace</a>.</p>
                    </div>
                    <div style='text-align:center;font-size:12px;color:#888;border-top:1px solid #f0f0f0;padding-top:20px'>
                        <p>This is an automated email, please do not reply directly.</p>
                        <p>&copy; Education Platform</p>
                    </div>
                </div>
            </body>
            </html>";
        }
    }
}
