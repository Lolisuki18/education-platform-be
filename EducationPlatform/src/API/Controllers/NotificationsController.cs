using Application.Features.Notifications;
using Application.Results;
using API.Models.Common;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    /// <summary>The signed-in user's own notifications. New ones also arrive live on the SignalR hub as a "Notification" event.</summary>
    [ApiController]
    [Authorize]
    [ApiVersion("1.0")]
    [Route("api/notifications")]
    [Route("api/v{version:apiVersion}/notifications")]
    public class NotificationsController : ControllerBase
    {
        private readonly IMediator mediator;

        public NotificationsController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResult<NotificationDTO>>>> List(
            [FromQuery] bool unreadOnly = false,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await mediator.Send(new GetNotificationsQuery
            {
                UnreadOnly = unreadOnly,
                PageIndex = pageIndex,
                PageSize = pageSize
            });

            return Ok(ApiResponse<PagedResult<NotificationDTO>>.Success(result));
        }

        [HttpGet("unread-count")]
        public async Task<ActionResult<ApiResponse<int>>> UnreadCount()
        {
            var count = await mediator.Send(new GetUnreadNotificationCountQuery());
            return Ok(ApiResponse<int>.Success(count));
        }

        [HttpPost("{id:guid}/read")]
        public async Task<ActionResult<ApiResponse>> MarkRead(Guid id)
        {
            await mediator.Send(new MarkNotificationReadCommand { NotificationId = id });
            return Ok(ApiResponse.Success("Notification marked as read."));
        }

        [HttpPost("read-all")]
        public async Task<ActionResult<ApiResponse<int>>> MarkAllRead()
        {
            var changed = await mediator.Send(new MarkAllNotificationsReadCommand());
            return Ok(ApiResponse<int>.Success(changed, "All notifications marked as read."));
        }
    }
}
