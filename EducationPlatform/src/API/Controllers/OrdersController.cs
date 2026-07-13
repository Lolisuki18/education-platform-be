using Application.Results;
using Application.Features.Courses.Queries.GetCourseDetail;
using Application.Features.Orders.Queries.GetCoupons;
using Application.Features.Orders.Queries.GetOrders;
using Application.Features.Orders.Commands.CreateOrder;
using Application.Features.Orders.Commands.FinishOrder;
using API.Models.Orders;
using API.Models.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Domain.OrderManagement.Enum;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;

namespace API.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrdersController : ControllerBase
    {
        private readonly IMediator mediator;
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment environment;

        public OrdersController(IMediator mediator, IConfiguration configuration, IWebHostEnvironment environment)
        {
            this.mediator = mediator;
            this.configuration = configuration;
            this.environment = environment;
        }

        [Authorize]
        [HttpGet("coupons")]
        public async Task<ActionResult<ApiResponse<IEnumerable<CouponDTO>>>> ListCoupons()
        {
            var coupons = await mediator.Send(new GetCouponsQuery());
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
            });

            return Ok(ApiResponse<ListOrdersResponseDto>.Success(new ListOrdersResponseDto
            {
                Orders = orders
            }));
        }

        [Authorize]
        [HttpGet("course/{courseId:guid}")]
        public async Task<ActionResult<ApiResponse<CourseDetailDTO>>> GetCourseForOrder(Guid courseId)
        {
            var course = await mediator.Send(new GetCourseDetailQuery { CourseID = courseId });
            return Ok(ApiResponse<CourseDetailDTO>.Success(course));
        }

        [Authorize]
        [HttpPost]
        public async Task<ActionResult<ApiResponse<CreateOrderResponseDto>>> CreateOrder([FromBody] CreateOrderRequestDto request)
        {
            var order = await mediator.Send(new CreateOrderCommand
            {
                CourseID = request.CourseId,
                CouponIds = request.SelectedCouponIds
            });

            return Ok(ApiResponse<CreateOrderResponseDto>.Success(new CreateOrderResponseDto
            {
                CheckoutUrl = order.CheckoutUrl
            }, "Order created successfully"));
        }

        [HttpGet("return")]
        public async Task<ActionResult<ApiResponse<ReturnOrderResponseDto>>> ReturnOrder(
            [FromQuery] string? status,
            [FromQuery] string? orderCode,
            [FromQuery] string? id,
            [FromQuery] string? code,
            [FromQuery] string? signature,
            [FromQuery] string? amount,
            [FromQuery] string? cancel)
        {
            bool cancelVal = string.Equals(cancel, "true", StringComparison.OrdinalIgnoreCase);
            var isSuccess = status == "PAID" && !cancelVal;
            long codeVal = long.TryParse(orderCode, out var parsedCode) ? parsedCode : 0;

            var isTesting = environment.IsEnvironment("Testing");
            var isDevelopment = environment.IsEnvironment("Development");
            if (!isTesting && !isDevelopment)
            {
                var checksumKey = configuration["PayOS:ChecksumKey"]!;
                if (string.IsNullOrEmpty(signature) ||
                    !VerifyRedirectSignature(amount ?? "", cancel ?? "", code ?? "", id ?? "", orderCode ?? "", status ?? "", signature, checksumKey))
                {
                    return BadRequest(ApiResponse<ReturnOrderResponseDto>.Success(new ReturnOrderResponseDto
                    {
                        OrderCode = codeVal,
                        Status = status ?? "",
                        IsSuccess = false,
                        Message = "Security verification failed. Invalid signature."
                    }, "Invalid signature"));
                }
            }

            var returnData = new ReturnOrderResponseDto
            {
                OrderCode = codeVal,
                Status = status ?? "",
                IsSuccess = isSuccess
            };

            if (isTesting)
            {
                if (isSuccess)
                {
                    await mediator.Send(new FinishOrderCommand { OrderCode = codeVal });
                    returnData.Message = "Payment successful! Your course is now available.";
                }
                else
                {
                    returnData.Message = "Payment was cancelled or failed. Please try again.";
                }

                return Ok(ApiResponse<ReturnOrderResponseDto>.Success(returnData,
                    isSuccess ? "Payment successful" : "Payment failed"));
            }

            // Đọc từ biến môi trường của Render
            var frontendUrl = configuration["PayOS:FrontendUrl"] ?? "http://localhost:3000";

            // Xóa bỏ ký tự gạch chéo ở cuối nếu lỡ tay điền dư trong config
            frontendUrl = frontendUrl.TrimEnd('/');

            if (isSuccess)
            {
                // Bọc lệnh hoàn tất đơn hàng ngầm hoặc xử lý nhanh để redirect ngay lập tức
                _ = Task.Run(async () =>
                {
                    try { await mediator.Send(new FinishOrderCommand { OrderCode = codeVal }); } catch { }
                });
                return Redirect($"{frontendUrl}/student?payment=success");
            }
            else
            {
                return Redirect($"{frontendUrl}/student?payment=cancelled");
            }
        }

        [AllowAnonymous]
        [HttpPost("webhook")]
        public IActionResult HandleWebhook([FromBody] Newtonsoft.Json.Linq.JObject payload)
        {
            try
            {
                // 1. Lấy dữ liệu an toàn từ payload
                var signature = payload["signature"]?.ToString();
                var code = payload["code"]?.ToString();
                var data = payload["data"] as Newtonsoft.Json.Linq.JObject;

                // 2. Trả lời PayOS ngay lập tức nếu đây chỉ là request Ping (test kết nối)
                if (payload == null || string.IsNullOrEmpty(signature) || data == null)
                {
                    return Ok(new { code = "00", desc = "success" });
                }

                // 3. Xác thực chữ ký
                var checksumKey = configuration["PayOS:ChecksumKey"]!;
                if (!VerifyWebhookSignature(data, signature, checksumKey))
                {
                    return BadRequest(new { code = "99", desc = "Invalid signature" });
                }

                // 4. Xử lý đơn hàng chạy NGẦM để không làm PayOS bị timeout 10s
                if (code == "00")
                {
                    var orderCode = data["orderCode"] != null ? (long)data["orderCode"] : (long?)null;
                    if (orderCode.HasValue)
                    {
                        // Task.Run giúp luồng này chạy độc lập dưới background (không dùng await ở đây)
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                // Tạo scope mới nếu mediator cần các service dạng Scoped
                                using var scope = environment.ApplicationServices.CreateScope();
                                var scopedMediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                                await scopedMediator.Send(new FinishOrderCommand { OrderCode = orderCode.Value });
                            }
                            catch (Exception)
                            {
                                // Bỏ qua lỗi ngầm để tránh sập app
                            }
                        });
                    }
                }

                // 5. Trả về đúng mã PayOS yêu cầu ngay lập tức
                return Ok(new { code = "00", desc = "success" });
            }
            catch (Exception ex)
            {
                return Ok(new { code = "99", desc = ex.Message });
            }
        }

        private bool VerifyWebhookSignature(
            Newtonsoft.Json.Linq.JObject data,
            string signature,
            string checksumKey)
        {
            // Sort keys alphabetically
            var sortedProperties = data.Properties()
                .Where(p => p.Name != "signature")
                .OrderBy(p => p.Name);

            // Construct the query string
            var queryString = string.Join("&", sortedProperties.Select(p =>
            {
                var val = p.Value;
                string strVal = "";
                if (val.Type == Newtonsoft.Json.Linq.JTokenType.Null)
                {
                    strVal = "";
                }
                else if (val.Type == Newtonsoft.Json.Linq.JTokenType.Boolean)
                {
                    strVal = (bool)val ? "true" : "false";
                }
                else
                {
                    strVal = val.ToString();
                }
                return $"{p.Name}={strVal}";
            }));

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(queryString));
            var computedSignature = BitConverter.ToString(hash).Replace("-", "").ToLower();

            return string.Equals(computedSignature, signature, StringComparison.OrdinalIgnoreCase);
        }

        private bool VerifyRedirectSignature(
            string amount,
            string cancel,
            string code,
            string id,
            string orderCode,
            string status,
            string signature,
            string checksumKey)
        {
            if (string.IsNullOrEmpty(signature)) return false;

            // Build the query string sorted alphabetically (amount is not returned in PayOS redirect)
            string raw = $"cancel={cancel.ToLower()}&code={code}&id={id}&orderCode={orderCode}&status={status}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
            var computedSignature = BitConverter.ToString(hash).Replace("-", "").ToLower();

            return string.Equals(computedSignature, signature, StringComparison.OrdinalIgnoreCase);
        }
    }
}
