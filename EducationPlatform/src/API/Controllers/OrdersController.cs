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
using Microsoft.AspNetCore.Hosting;
using Application.Interface;

namespace API.Controllers
{
    [ApiController]
    [Route("api/orders")]
    public class OrdersController : ControllerBase
    {
        private readonly IMediator mediator;
        private readonly IConfiguration configuration;
        private readonly IWebHostEnvironment environment;
        private readonly IServiceScopeFactory scopeFactory;
        private readonly ILogger<OrdersController> logger;
        private readonly IPayOSSignatureVerifier signatureVerifier;

        public OrdersController(
            IMediator mediator,
            IConfiguration configuration,
            IWebHostEnvironment environment,
            IServiceScopeFactory scopeFactory,
            ILogger<OrdersController> logger,
            IPayOSSignatureVerifier signatureVerifier)
        {
            this.mediator = mediator;
            this.configuration = configuration;
            this.environment = environment;
            this.scopeFactory = scopeFactory;
            this.logger = logger;
            this.signatureVerifier = signatureVerifier;
        }

        private void FinishOrderInBackground(long orderCode)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    var scopedMediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                    await scopedMediator.Send(new FinishOrderCommand { OrderCode = orderCode });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to finalize order {OrderCode} in background.", orderCode);
                }
            });
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

            var checksumKey = configuration["PayOS:ChecksumKey"];
            var isTesting = environment.IsEnvironment("Testing");

            if (!isTesting)
            {
                if (string.IsNullOrEmpty(checksumKey) || checksumKey == "YOUR_PAYOS_CHECKSUM_KEY")
                {
                    return BadRequest(ApiResponse<ReturnOrderResponseDto>.Success(new ReturnOrderResponseDto
                    {
                        OrderCode = codeVal,
                        Status = status ?? "",
                        IsSuccess = false,
                        Message = "Payment signature verification failed. Missing configuration."
                    }, "PayOS ChecksumKey is missing."));
                }

                if (string.IsNullOrEmpty(signature) ||
                    !signatureVerifier.VerifyRedirectSignature(amount ?? "", cancel ?? "", code ?? "", id ?? "", orderCode ?? "", status ?? "", signature, checksumKey))
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

            var frontendUrl = configuration["PayOS:FrontendUrl"] ?? "http://localhost:3000";

            frontendUrl = frontendUrl.TrimEnd('/');

            if (isSuccess)
            {
                FinishOrderInBackground(codeVal);
                return Redirect($"{frontendUrl}/student?payment=success");
            }
            else
            {
                return Redirect($"{frontendUrl}/student?payment=cancelled");
            }
        }

        [AllowAnonymous]
        [HttpPost("webhook")]
        public async Task<IActionResult> HandleWebhook()
        {
            try
            {
                using var reader = new System.IO.StreamReader(Request.Body);
                var bodyString = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(bodyString))
                {
                    return Ok(new { code = "00", desc = "success" });
                }

                var payload = Newtonsoft.Json.Linq.JObject.Parse(bodyString);
                var signature = payload["signature"]?.ToString();
                var code = payload["code"]?.ToString();
                var data = payload["data"] as Newtonsoft.Json.Linq.JObject;

                if (string.IsNullOrEmpty(signature) || data == null)
                {
                    return Ok(new { code = "00", desc = "success" });
                }

                var checksumKey = configuration["PayOS:ChecksumKey"]!;
                var dataDict = data.Properties()
                    .ToDictionary(p => p.Name, p => JTokenToString(p.Value));

                if (!signatureVerifier.VerifyWebhookSignature(dataDict, signature, checksumKey))
                {
                    return Ok(new { code = "99", desc = "Invalid signature" });
                }

                if (code == "00")
                {
                    var orderCode = data["orderCode"] != null ? (long)data["orderCode"] : (long?)null;
                    if (orderCode.HasValue)
                    {
                        FinishOrderInBackground(orderCode.Value);
                    }
                }

                return Ok(new { code = "00", desc = "success" });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process PayOS webhook payload.");
                return Ok(new { code = "99", desc = ex.Message });
            }
        }

        private static string JTokenToString(Newtonsoft.Json.Linq.JToken val)
        {
            return val.Type switch
            {
                Newtonsoft.Json.Linq.JTokenType.Null => "",
                Newtonsoft.Json.Linq.JTokenType.Boolean => (bool)val ? "true" : "false",
                _ => val.ToString()
            };
        }
    }
}
