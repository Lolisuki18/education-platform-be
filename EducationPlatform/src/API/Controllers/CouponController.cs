using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using API.Models.Common;
using API.Models.Coupons;
using API.Helper;
using Application.Features.Coupons.Queries.GetCoupons;
using Application.Features.Coupons.Queries.GetCouponById;
using Application.Features.Coupons.Queries.GetAvailableCoupons;
using Application.Features.Coupons.Commands.CreateCoupon;
using Application.Features.Coupons.Commands.UpdateCoupon;
using Application.Features.Coupons.Commands.UpdateCouponStatus;
using Application.Results;
using Domain.OrderManagement.Enum;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers
{
    [ApiController]
    [Route("api")]
    public class CouponController : ControllerBase
    {
        private readonly IMediator mediator;

        public CouponController(IMediator mediator)
        {
            this.mediator = mediator;
        }

        [HttpGet("coupons/available")]
        public async Task<ActionResult<ApiResponse<IEnumerable<CouponDTO>>>> GetAvailableCoupons()
        {
            var result = await mediator.Send(new GetAvailableCouponsQuery());
            return Ok(ApiResponse<IEnumerable<CouponDTO>>.Success(result));
        }

        [Authorize(Policy = Policies.AdminOnly)]
        [HttpGet("admin/coupons")]
        public async Task<ActionResult<ApiResponse<PagedResult<CouponDTO>>>> GetCoupons(
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] CouponType? type = null)
        {
            var result = await mediator.Send(new GetCouponsQuery
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                Search = search,
                IsActive = isActive,
                Type = type
            });
            return Ok(ApiResponse<PagedResult<CouponDTO>>.Success(result));
        }

        [Authorize(Policy = Policies.AdminOnly)]
        [HttpGet("admin/coupons/{id}")]
        public async Task<ActionResult<ApiResponse<CouponDTO>>> GetCouponDetails(Guid id)
        {
            var result = await mediator.Send(new GetCouponByIdQuery { CouponId = id });
            return Ok(ApiResponse<CouponDTO>.Success(result));
        }

        [Authorize(Policy = Policies.AdminOnly)]
        [HttpPost("admin/coupons")]
        public async Task<ActionResult<ApiResponse<Guid>>> CreateCoupon([FromBody] CreateCouponRequest request)
        {
            var command = new CreateCouponCommand
            {
                Code = request.Code,
                Description = request.Description,
                DiscountAmount = request.DiscountAmount,
                StartDate = request.StartDate,
                ExpiredDate = request.ExpiredDate,
                MaxUsage = request.MaxUsage
            };
            var id = await mediator.Send(command);
            return StatusCode(201, ApiResponse<Guid>.Success(id, "Coupon created successfully", 201));
        }

        [Authorize(Policy = Policies.AdminOnly)]
        [HttpPut("admin/coupons/{id}")]
        public async Task<ActionResult<ApiResponse>> UpdateCoupon(Guid id, [FromBody] UpdateCouponRequest request)
        {
            var command = new UpdateCouponCommand
            {
                CouponId = id,
                Description = request.Description,
                DiscountAmount = request.DiscountAmount,
                StartDate = request.StartDate,
                ExpiredDate = request.ExpiredDate,
                MaxUsage = request.MaxUsage
            };
            await mediator.Send(command);
            return Ok(ApiResponse.Success("Coupon updated successfully"));
        }

        [Authorize(Policy = Policies.AdminOnly)]
        [HttpPut("admin/coupons/{id}/status")]
        public async Task<ActionResult<ApiResponse>> UpdateCouponStatus(Guid id, [FromBody] UpdateCouponStatusRequest request)
        {
            var command = new UpdateCouponStatusCommand
            {
                CouponId = id,
                IsActive = request.IsActive
            };
            await mediator.Send(command);
            return Ok(ApiResponse.Success("Coupon status updated successfully"));
        }
    }
}
