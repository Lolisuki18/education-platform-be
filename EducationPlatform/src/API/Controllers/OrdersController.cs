using Asp.Versioning;
using Application.Results;
using Application.Features.Courses.Queries.GetCourseDetail;
using Application.Features.Orders.Queries.GetMyCoupons;
using Application.Features.Orders.Queries.GetOrders;
using Application.Features.Orders.Commands.CreateOrder;
using Application.Features.Orders.Commands.ProcessPayOSReturn;
using Application.Features.Orders.Commands.ProcessPayOSWebhook;
using Application.Options;
using API.Models.Orders;
using API.Models.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Domain.OrderManagement.Enum;

namespace API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/orders")]
    [Route("api/v{version:apiVersion}/orders")]
    public class OrdersController : ControllerBase
    {
        private readonly IMediator mediator;
        private readonly PayOSOptions payOSOptions;

        public OrdersController(
            IMediator mediator,
            IOptions<PayOSOptions> payOSOptions)
        {
            this.mediator = mediator;
            this.payOSOptions = payOSOptions.Value;
        }

        private string FrontendUrl => payOSOptions.FrontendUrl.TrimEnd('/');

        [Authorize]
        [HttpGet("coupons")]
        public async Task<ActionResult<ApiResponse<IEnumerable<CouponDTO>>>> ListCoupons()
        {
            var coupons = await mediator.Send(new GetMyCouponsQuery(), HttpContext.RequestAborted);
            return Ok(ApiResponse<IEnumerable<CouponDTO>>.Success(coupons));
        }

        [Authorize]
        [HttpGet]
        public async Task<ActionResult<ApiResponse<ListOrdersResponseDto>>> ListOrders([FromQuery] ListOrdersRequestDto request)
        {
            var orders = await mediator.Send(new GetOrdersQuery
            {
                OrderStatus = Enum.TryParse<OrderStatus>(request.Status, true, out var s) ? s : null,
                PageIndex = request.Page,
                PageSize = request.PageSize
            }, HttpContext.RequestAborted);

            return Ok(ApiResponse<ListOrdersResponseDto>.Success(new ListOrdersResponseDto
            {
                Orders = orders
            }));
        }

        [Authorize]
        [HttpGet("course/{courseId:guid}")]
        public async Task<ActionResult<ApiResponse<CourseDetailDTO>>> GetCourseForOrder(Guid courseId)
        {
            var course = await mediator.Send(new GetCourseDetailQuery { CourseID = courseId }, HttpContext.RequestAborted);
            return Ok(ApiResponse<CourseDetailDTO>.Success(course));
        }

        // Only students can study (the enrollment endpoints are student-only), so anyone else would pay for a course they can never open
        [Authorize(Roles = "Student")]
        [HttpPost]
        public async Task<ActionResult<ApiResponse<CreateOrderResponseDto>>> CreateOrder([FromBody] CreateOrderRequestDto request)
        {
            var order = await mediator.Send(new CreateOrderCommand
            {
                CourseID = request.CourseId,
                CouponIds = request.SelectedCouponIds
            });

            // A free (or fully discounted) order is paid already: send the client to the same place a payment would
            var requiresPayment = order.Status == OrderStatus.Created;

            return Ok(ApiResponse<CreateOrderResponseDto>.Success(new CreateOrderResponseDto
            {
                CheckoutUrl = requiresPayment
                    ? order.CheckoutUrl ?? string.Empty
                    : $"{FrontendUrl}/student?payment=success",
                RequiresPayment = requiresPayment
            }, "Order created successfully"));
        }

        /// <summary>Where PayOS sends the browser after the payment page. Verifies the signature, then redirects to the frontend.</summary>
        [AllowAnonymous]
        [HttpGet("return")]
        public async Task<IActionResult> ReturnOrder([FromQuery] ProcessPayOSReturnCommand request)
        {
            var result = await mediator.Send(request);

            if (!result.IsSignatureValid)
            {
                return BadRequest(ApiResponse.Error("Security verification failed. Invalid signature."));
            }

            return Redirect(result.IsSuccess
                ? $"{FrontendUrl}/student?payment=success"
                : $"{FrontendUrl}/student?payment=cancelled");
        }

        /// <summary>
        /// PayOS server-to-server notification. Failures surface as 5xx on purpose so PayOS retries delivery.
        /// </summary>
        [AllowAnonymous]
        [HttpPost("webhook")]
        public async Task<IActionResult> HandleWebhook()
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            var result = await mediator.Send(new ProcessPayOSWebhookCommand { Body = body });

            return result switch
            {
                PayOSWebhookResult.InvalidSignature => BadRequest(new { code = "99", desc = "Invalid signature" }),
                PayOSWebhookResult.Malformed => BadRequest(new { code = "99", desc = "Malformed payload" }),
                _ => Ok(new { code = "00", desc = "success" })
            };
        }
    }
}
