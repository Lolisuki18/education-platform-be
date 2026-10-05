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
            var returnResponse = await ReturnFromPayOsAsync(dbOrder.OrderCode);

            // Assert: the browser is sent back to the frontend with a success flag
            returnResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
            returnResponse.Headers.Location!.ToString().Should().Contain("payment=success");

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
            var returnResponse = await ReturnFromPayOsAsync(dbOrder!.OrderCode);
            returnResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

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

        [Fact]
        public async Task CreateOrder_Twice_ReusesTheOrderAwaitingPayment()
        {
            await LoginExistingUserAsync("student@example.com", "Password123!");

            Course? course = null;
            await ExecuteDbContextAsync(async db =>
            {
                course = await db.Set<Course>().FirstOrDefaultAsync(c => c.Title == "Math algebra");
            });

            var request = new CreateOrderRequestDto { CourseId = course!.CourseID, SelectedCouponIds = new List<Guid>() };

            var first = await Client.PostAsJsonAsync("/api/orders", request);
            var second = await Client.PostAsJsonAsync("/api/orders", request);

            first.StatusCode.Should().Be(HttpStatusCode.OK);
            second.StatusCode.Should().Be(HttpStatusCode.OK);

            var orders = await ExecuteDbContextAsync(db => db.Set<Order>().Where(o => o.CourseID == course.CourseID).ToListAsync());
            orders.Should().ContainSingle();
            orders[0].CheckoutUrl.Should().Be("https://mock-payment-url.com");
        }

        [Fact]
        public async Task ReturnFromPayOs_WhenCancelled_RedirectsToCancelledAndLeavesOrderUnpaid()
        {
            await LoginExistingUserAsync("student@example.com", "Password123!");

            Course? course = null;
            await ExecuteDbContextAsync(async db =>
            {
                course = await db.Set<Course>().FirstOrDefaultAsync(c => c.Title == "Math algebra");
            });

            await Client.PostAsJsonAsync("/api/orders", new CreateOrderRequestDto { CourseId = course!.CourseID, SelectedCouponIds = new List<Guid>() });
            var order = await ExecuteDbContextAsync(db => db.Set<Order>().FirstAsync(o => o.CourseID == course.CourseID));

            var response = await ReturnFromPayOsAsync(order.OrderCode, status: "CANCELLED", cancelled: true);

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location!.ToString().Should().Contain("payment=cancelled");

            var after = await ExecuteDbContextAsync(db => db.Set<Order>().FirstAsync(o => o.OrderID == order.OrderID));
            after.Status.Should().Be(OrderStatus.Created);
        }

        [Fact]
        public async Task Webhook_WithPaidPayload_FinishesTheOrderAndIsIdempotent()
        {
            await LoginExistingUserAsync("student@example.com", "Password123!");

            Course? course = null;
            await ExecuteDbContextAsync(async db =>
            {
                course = await db.Set<Course>().FirstOrDefaultAsync(c => c.Title == "Math algebra");
            });

            await Client.PostAsJsonAsync("/api/orders", new CreateOrderRequestDto { CourseId = course!.CourseID, SelectedCouponIds = new List<Guid>() });
            var order = await ExecuteDbContextAsync(db => db.Set<Order>().FirstAsync(o => o.CourseID == course.CourseID));

            var payload = new
            {
                code = "00",
                desc = "success",
                success = true,
                data = new { orderCode = order.OrderCode, amount = 50000, description = "Thanh toan" },
                signature = "any-signature"
            };

            // PayOS may deliver the same webhook more than once
            var first = await Client.PostAsJsonAsync("/api/orders/webhook", payload);
            var second = await Client.PostAsJsonAsync("/api/orders/webhook", payload);

            first.StatusCode.Should().Be(HttpStatusCode.OK);
            second.StatusCode.Should().Be(HttpStatusCode.OK);

            var after = await ExecuteDbContextAsync(db => db.Set<Order>().FirstAsync(o => o.OrderID == order.OrderID));
            after.Status.Should().Be(OrderStatus.Pending);

            var enrollments = await ExecuteDbContextAsync(db => db.Set<Domain.EnrollmentManagement.Aggregate.Enrollment>()
                .Where(e => e.CourseID == course.CourseID).ToListAsync());
            enrollments.Should().ContainSingle();
        }

        [Fact]
        public async Task Webhook_WithWrongAmount_DoesNotFinishTheOrder()
        {
            await LoginExistingUserAsync("student@example.com", "Password123!");

            Course? course = null;
            await ExecuteDbContextAsync(async db =>
            {
                course = await db.Set<Course>().FirstOrDefaultAsync(c => c.Title == "Math algebra");
            });

            await Client.PostAsJsonAsync("/api/orders", new CreateOrderRequestDto { CourseId = course!.CourseID, SelectedCouponIds = new List<Guid>() });
            var order = await ExecuteDbContextAsync(db => db.Set<Order>().FirstAsync(o => o.CourseID == course.CourseID));

            var response = await Client.PostAsJsonAsync("/api/orders/webhook", new
            {
                code = "00",
                data = new { orderCode = order.OrderCode, amount = 1000 },
                signature = "any-signature"
            });

            // Acknowledged (nothing to retry) but the order stays unpaid
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var after = await ExecuteDbContextAsync(db => db.Set<Order>().FirstAsync(o => o.OrderID == order.OrderID));
            after.Status.Should().Be(OrderStatus.Created);
        }

        [Fact]
        public async Task Webhook_WithMalformedBody_ReturnsBadRequest()
        {
            var response = await Client.PostAsync("/api/orders/webhook",
                new System.Net.Http.StringContent("this is not json", System.Text.Encoding.UTF8, "application/json"));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Webhook_ForUnknownOrder_IsAcknowledged()
        {
            // PayOS sends such a payload when the webhook URL is registered
            var response = await Client.PostAsJsonAsync("/api/orders/webhook", new
            {
                code = "00",
                data = new { orderCode = 123, amount = 3000 },
                signature = "any-signature"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task CreateOrder_WhenCouponCoversThePrice_EnrollsWithoutPayment()
        {
            await LoginExistingUserAsync("student@example.com", "Password123!");

            Course? course = null;
            Guid couponId = Guid.Empty;
            await ExecuteDbContextAsync(async db =>
            {
                course = await db.Set<Course>().FirstOrDefaultAsync(c => c.Title == "Math algebra");

                var coupon = new Coupon(Guid.NewGuid(), "FREEALL", "Covers the whole course", 1_000_000m,
                    DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(10), 5);
                db.Set<Coupon>().Add(coupon);
                await db.SaveChangesAsync();
                couponId = coupon.CouponID;
            });

            var response = await Client.PostAsJsonAsync("/api/orders",
                new CreateOrderRequestDto { CourseId = course!.CourseID, SelectedCouponIds = new List<Guid> { couponId } });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<API.Models.Common.ApiResponse<CreateOrderResponseDto>>();
            result!.Data!.RequiresPayment.Should().BeFalse();
            result.Data.CheckoutUrl.Should().Contain("payment=success");

            var order = await ExecuteDbContextAsync(db => db.Set<Order>().FirstAsync(o => o.CourseID == course.CourseID));
            order.Status.Should().Be(OrderStatus.Pending);

            var enrolled = await ExecuteDbContextAsync(db => db.Set<Domain.EnrollmentManagement.Aggregate.Enrollment>()
                .AnyAsync(e => e.CourseID == course.CourseID));
            enrolled.Should().BeTrue();
        }
    }
}
