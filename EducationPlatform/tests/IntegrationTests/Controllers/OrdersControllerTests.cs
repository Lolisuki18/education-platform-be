using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Orders;
using Domain.CourseManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Enum;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Application.Results;

namespace IntegrationTests.Controllers
{
    public class OrdersControllerTests : IntegrationTestBase
    {
        public OrdersControllerTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task ListCoupons_ReturnsCouponsForStudent()
        {
            // Arrange
            await LoginExistingUserAsync("student@example.com", "Password123!");

            // Act
            var response = await Client.GetAsync("/api/orders/coupons");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<API.Models.Common.ApiResponse<IEnumerable<CouponDTO>>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Data.Should().ContainSingle(c => c.Code == "DISCOUNT10");
        }

        [Fact]
        public async Task CreateOrderAndCompletePayment_Success()
        {
            // Arrange
            await LoginExistingUserAsync("student@example.com", "Password123!");

            Course? course = null;
            Coupon? coupon = null;
            await ExecuteDbContextAsync(async db =>
            {
                course = await db.Set<Course>().FirstOrDefaultAsync(c => c.Title == "Math algebra");
                coupon = await db.Set<Coupon>().FirstOrDefaultAsync(c => c.Code == "DISCOUNT10");
            });

            course.Should().NotBeNull();
            coupon.Should().NotBeNull();

            var request = new CreateOrderRequestDto
            {
                CourseId = course!.CourseID,
                SelectedCouponIds = new List<Guid> { coupon!.CouponID }
            };

            // Act: Create Order
            var createResponse = await Client.PostAsJsonAsync("/api/orders", request);

            // Assert: Create Order
            createResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var createResult = await createResponse.Content.ReadFromJsonAsync<API.Models.Common.ApiResponse<CreateOrderResponseDto>>();
            createResult.Should().NotBeNull();
            createResult!.IsSuccess.Should().BeTrue();
            createResult.Data.CheckoutUrl.Should().Be("https://mock-payment-url.com");

            // Find the order code from DB
            Order? dbOrder = null;
            await ExecuteDbContextAsync(db =>
            {
                dbOrder = db.Set<Order>().FirstOrDefault(o => o.CourseID == course.CourseID);
                return Task.CompletedTask;
            });
            dbOrder.Should().NotBeNull();
            dbOrder!.Status.Should().Be(OrderStatus.Created);

            // Act: Return Payment Success
            var returnResponse = await Client.GetAsync($"/api/orders/return?status=PAID&orderCode={dbOrder.OrderCode}");

            // Assert: Return Payment Success
            returnResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var returnResult = await returnResponse.Content.ReadFromJsonAsync<API.Models.Common.ApiResponse<ReturnOrderResponseDto>>();
            returnResult.Should().NotBeNull();
            returnResult!.IsSuccess.Should().BeTrue();
            returnResult.Data.IsSuccess.Should().BeTrue();

            // Assert: Order updated to Pending (Paid in business flow) and Coupon marked as used
            await ExecuteDbContextAsync(async db =>
            {
                var updatedOrder = await db.Set<Order>().FindAsync(dbOrder.OrderID);
                updatedOrder!.Status.Should().Be(OrderStatus.Pending);

                var updatedCoupon = await db.Set<Coupon>().FindAsync(coupon.CouponID);
                updatedCoupon!.IsUsed.Should().BeTrue();
            });
        }

        [Fact]
        public async Task CreateOrder_WhenAlreadyEnrolled_ReturnsConflict()
        {
            // Arrange
            await LoginExistingUserAsync("student@example.com", "Password123!");

            Course? course = null;
            await ExecuteDbContextAsync(async db =>
            {
                course = await db.Set<Course>().FirstOrDefaultAsync(c => c.Title == "Math algebra");
            });
            course.Should().NotBeNull();

            // Create a completed order to trigger enrollment
            var request = new CreateOrderRequestDto
            {
                CourseId = course!.CourseID,
                SelectedCouponIds = new List<Guid>()
            };

            var createResponse = await Client.PostAsJsonAsync("/api/orders", request);
            createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            Order? dbOrder = null;
            await ExecuteDbContextAsync(db =>
            {
                dbOrder = db.Set<Order>().FirstOrDefault(o => o.CourseID == course.CourseID);
                return Task.CompletedTask;
            });
            dbOrder.Should().NotBeNull();

            // Finish the order so the student is enrolled
            var returnResponse = await Client.GetAsync($"/api/orders/return?status=PAID&orderCode={dbOrder!.OrderCode}");
            returnResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Act: Attempt to purchase the SAME course again
            var duplicateResponse = await Client.PostAsJsonAsync("/api/orders", request);

            // Assert: Reject with 409 Conflict
            duplicateResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task CreateOrder_WithInvalidCoupon_IgnoresCoupon()
        {
            // Arrange
            await LoginExistingUserAsync("student@example.com", "Password123!");

            Course? course = null;
            await ExecuteDbContextAsync(async db =>
            {
                course = await db.Set<Course>().FirstOrDefaultAsync(c => c.Title == "Math algebra");
            });
            course.Should().NotBeNull();

            var request = new CreateOrderRequestDto
            {
                CourseId = course!.CourseID,
                SelectedCouponIds = new List<Guid> { Guid.NewGuid() } // Nonexistent coupon ID
            };

            // Act
            var response = await Client.PostAsJsonAsync("/api/orders", request);

            // Assert: Order creation succeeds but coupon is skipped/ignored
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<API.Models.Common.ApiResponse<CreateOrderResponseDto>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();

            // Verify order was created with NO discount (full price)
            Order? dbOrder = null;
            await ExecuteDbContextAsync(db =>
            {
                dbOrder = db.Set<Order>().FirstOrDefault(o => o.CourseID == course.CourseID);
                return Task.CompletedTask;
            });
            dbOrder.Should().NotBeNull();
            dbOrder!.PlatformAmount.Should().Be(7500); // 15% platform fee on 50000 course price = 7500
        }
    }
}
