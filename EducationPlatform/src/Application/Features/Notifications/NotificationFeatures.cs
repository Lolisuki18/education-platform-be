using Application.Exceptions;
using Application.Interface;
using Application.Results;
using Domain.Common.Interfaces;
using Domain.NotificationManagement.Aggregate;
using MediatR;

namespace Application.Features.Notifications
{
    public class NotificationDTO
    {
        public Guid NotificationID { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }

    // ---------------------------------------------------------------- list

    public class GetNotificationsQuery : IRequest<PagedResult<NotificationDTO>>
    {
        public bool UnreadOnly { get; set; }

        private int _pageIndex = 1;
        public int PageIndex
        {
            get => _pageIndex;
            set => _pageIndex = Common.Paging.NormalizePageIndex(value);
        }

        private int _pageSize = Common.Paging.DefaultPageSize;
        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = Common.Paging.NormalizePageSize(value);
        }
    }

    public class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, PagedResult<NotificationDTO>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public GetNotificationsQueryHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<PagedResult<NotificationDTO>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var (items, total) = await _unitOfWork.GetRepository<INotificationRepository>()
                .GetForUserAsync(_currentUser.Id.Value, request.UnreadOnly, request.PageIndex, request.PageSize, cancellationToken);

            return new PagedResult<NotificationDTO>
            {
                Items = items.Select(ToDto).ToList().AsReadOnly(),
                PageIndex = request.PageIndex,
                PageSize = request.PageSize,
                TotalItems = total
            };
        }

        internal static NotificationDTO ToDto(Notification n) => new()
        {
            NotificationID = n.NotificationID,
            Title = n.Title,
            Message = n.Message,
            CreatedAt = n.CreatedAt,
            IsRead = n.IsRead
        };
    }

    // ---------------------------------------------------------------- unread badge

    public class GetUnreadNotificationCountQuery : IRequest<int>
    {
    }

    public class GetUnreadNotificationCountQueryHandler : IRequestHandler<GetUnreadNotificationCountQuery, int>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public GetUnreadNotificationCountQueryHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<int> Handle(GetUnreadNotificationCountQuery request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            return await _unitOfWork.GetRepository<INotificationRepository>()
                .CountUnreadAsync(_currentUser.Id.Value, cancellationToken);
        }
    }

    // ---------------------------------------------------------------- mark read

    public class MarkNotificationReadCommand : IRequest<Unit>
    {
        public Guid NotificationId { get; set; }
    }

    public class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, Unit>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public MarkNotificationReadCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<Unit> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            var notification = await _unitOfWork.GetRepository<INotificationRepository>()
                .GetByIdAsync(request.NotificationId, cancellationToken);

            // Somebody else's notification looks exactly like one that does not exist
            if (notification == null || notification.UserID != _currentUser.Id.Value)
                throw new NotFoundException("Notification not found.");

            notification.MarkAsRead();
            await _unitOfWork.CommitAsync();

            return Unit.Value;
        }
    }

    public class MarkAllNotificationsReadCommand : IRequest<int>
    {
    }

    public class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand, int>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public MarkAllNotificationsReadCommandHandler(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        public async Task<int> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
        {
            if (!_currentUser.Id.HasValue)
                throw new AuthenticateException("User must be authenticated.");

            return await _unitOfWork.GetRepository<INotificationRepository>()
                .MarkAllAsReadAsync(_currentUser.Id.Value, cancellationToken);
        }
    }
}
