using Application.Results;
using Application.Features.Courses.Queries.GetCourseDetail;
using Application.Features.Orders.Queries.GetCoupons;
using Application.Features.Orders.Queries.GetOrders;
using Application.Features.Orders.Commands.CreateOrder;
using Application.Features.Orders.Commands.FinishOrder;
using API.Models.Orders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Domain.OrderManagement.Enum;

namespace API.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrdersController : ControllerBase
    {
        private readonly IMediator mediator;

        public OrdersController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        [Authorize]
        [HttpGet("coupons")]
        public async Task<ActionResult<IEnumerable<CouponDTO>>> ListCoupons()
        {
            var coupons = await mediator.Send(new GetCouponsQuery());
            return Ok(coupons);
        }

        [Authorize]
        [HttpGet]
        public async Task<ActionResult<ListOrdersResponseDto>> ListOrders([FromQuery] ListOrdersRequestDto request)
        {
            var orders = await mediator.Send(new GetOrdersQuery
            {
                OrderStatus = Enum.TryParse<OrderStatus>(request.Status, true, out var s) ? s : null,
                PageIndex   = request.Page,
                PageSize    = request.PageSize
            });

            return Ok(new ListOrdersResponseDto
            {
                Orders = orders
            });
        }

        [Authorize]
        [HttpGet("course/{courseId:guid}")]
        public async Task<ActionResult<CourseDetailDTO>> GetCourseForOrder(Guid courseId)
        {
            var course = await mediator.Send(new GetCourseDetailQuery { CourseID = courseId });
            return Ok(course);
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<CreateOrderResponseDto>> CreateOrder([FromBody] CreateOrderRequestDto request)
        {
            var order = await mediator.Send(new CreateOrderCommand
            {
                CourseID  = request.CourseId,
                CouponIds = request.SelectedCouponIds
            });

            return Ok(new CreateOrderResponseDto
            {
                CheckoutUrl = order.CheckoutUrl
            });
        }

        [Authorize]
        [HttpGet("return")]
        public async Task<ActionResult<ReturnOrderResponseDto>> ReturnOrder(
            [FromQuery] string status,
            [FromQuery] long orderCode,
            [FromQuery] bool cancel = false)
        {
            var response = new ReturnOrderResponseDto
            {
                OrderCode = orderCode,
                Status = status,
                IsSuccess = status == "PAID" && !cancel
            };

            if (response.IsSuccess)
            {
                await mediator.Send(new FinishOrderCommand { OrderCode = orderCode });
                response.Message = "Payment successful! Your course is now available.";
            }
            else
            {
                response.Message = "Payment was cancelled or failed. Please try again.";
            }

            return Ok(response);
        }
    }
}
