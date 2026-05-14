using System.Security.Cryptography;
using System.Text;
using Application.Results;
using Application.Features.Courses.Queries.GetCourseDetail;
using Application.Features.Orders.Queries.GetCoupons;
using Application.Features.Orders.Queries.GetOrders;
using Application.Features.Orders.Commands.CreateOrder;
using Application.Features.Orders.Commands.FinishOrder;
using API.Helper;
using API.Models.Orders;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Domain.OrderManagement.Enum;

namespace API.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrdersController : ControllerBase
    {
        private readonly HttpClient httpClient;
        private readonly IConfiguration config;
        private readonly IMediator mediator;

        public OrdersController(
            IConfiguration config,
            IHttpClientFactory factory,
            IMediator mediator)
        {
            this.config = config;
            httpClient = factory.CreateClient("PayOSClient");
            this.mediator = mediator;
        }

        [Authorize]
        [HttpGet("coupons")]
        public async Task<ActionResult<IEnumerable<CouponDTO>>> ListCoupons()
        {
            var (userId, role) = CheckClaimHelper.CheckClaim(User);
            var coupons = await mediator.Send(new GetCouponsQuery 
            { 
                CallerId = userId, 
                CallerRole = role 
            });
            return Ok(coupons);
        }

        [Authorize]
        [HttpGet]
        public async Task<ActionResult<ListOrdersResponseDto>> ListOrders([FromQuery] ListOrdersRequestDto request)
        {
            var (userId, role) = CheckClaimHelper.CheckClaim(User);

            var orders = await mediator.Send(new GetOrdersQuery
            {
                OrderStatus = Enum.TryParse<OrderStatus>(request.Status, true, out var s) ? s : null,
                PageIndex   = request.Page,
                PageSize    = request.PageSize,
                CallerId    = userId,
                CallerRole  = role
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
            var (userId, role) = CheckClaimHelper.CheckClaim(User);
            var course = await mediator.Send(new GetCourseDetailQuery
            {
                CourseID   = courseId,
                CallerId   = userId,
                CallerRole = role
            });
            return Ok(course);
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<CreateOrderResponseDto>> CreateOrder([FromBody] CreateOrderRequestDto request)
        {
            var (userId, role) = CheckClaimHelper.CheckClaim(User);

            var order = await mediator.Send(new CreateOrderCommand
            {
                CourseID  = request.CourseId,
                StudentID = userId,
                CouponIds = request.SelectedCouponIds
            });

            var payos = config.GetSection("PayOS");

            long orderCode = order.OrderCode;
            int amount = (int)(order.PlatformAmount + order.TeacherAmount);
            string description = "CourseOrder";

            string signature = GenerateSignature(
                orderCode,
                amount,
                description,
                payos["ReturnUrl"]!,
                payos["CancelUrl"]!,
                payos["ChecksumKey"]!
            );

            long expiredAt = DateTimeOffset.UtcNow
                .AddMinutes(15)
                .ToUnixTimeSeconds();

            var payload = new
            {
                orderCode,
                amount,
                description,
                cancelUrl = payos["CancelUrl"],
                returnUrl = payos["ReturnUrl"],
                expiredAt,
                signature
            };

            var json = JsonConvert.SerializeObject(payload);

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("x-client-id", payos["ClientId"]);
            httpClient.DefaultRequestHeaders.Add("x-api-key", payos["ApiKey"]);

            var requestMessage = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api-merchant.payos.vn/v2/payment-requests")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            requestMessage.Headers.Add("x-client-id", payos["ClientId"]);
            requestMessage.Headers.Add("x-api-key", payos["ApiKey"]);
            requestMessage.Headers.Add("accept", "application/json");

            var response = await httpClient.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync();
                throw new Exception($"PayOS Error: {response.StatusCode} - {errorDetails}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(responseJson);

            string checkoutUrl = result?.data?.checkoutUrl;

            return Ok(new CreateOrderResponseDto
            {
                CheckoutUrl = checkoutUrl
            });
        }

        [Authorize]
        [HttpGet("return")]
        public async Task<ActionResult<ReturnOrderResponseDto>> ReturnOrder(
            [FromQuery] string code,
            [FromQuery] string id,
            [FromQuery] bool cancel,
            [FromQuery] string status,
            [FromQuery] long orderCode)
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

        private static string GenerateSignature(
            long orderCode,
            int amount,
            string description,
            string returnUrl,
            string cancelUrl,
            string checksumKey)
        {
            string raw =
                $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
}
