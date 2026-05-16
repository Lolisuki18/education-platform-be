using Domain.OrderManagement.Events;
using MediatR;
using Domain.Common.Interfaces;
using Domain.EnrollmentManagement.Aggregate;

namespace Application.Features.Orders.EventHandlers
{
    public class OrderPaidEventHandler : INotificationHandler<OrderPaidEvent>
    {
        private readonly IUnitOfWork _unitOfWork;

        public OrderPaidEventHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
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

            await Task.CompletedTask;
        }
    }
}
