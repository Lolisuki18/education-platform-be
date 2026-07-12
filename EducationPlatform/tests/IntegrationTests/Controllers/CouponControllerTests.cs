using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Common;
using API.Models.Coupons;
using Application.Results;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Enum;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IntegrationTests.Controllers
{
    public class CouponControllerTests : IntegrationTestBase
    {
        public CouponControllerTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task GetAvailableCoupons_PublicEndpoint_ReturnsOnlyActiveMarketingCoupons()
        {
            // Arrange
            await Factory.ResetDatabaseAsync();

            await ExecuteDbContextAsync(async db =>
            {
                // Create multiple coupons:
                // 1. Valid Marketing Coupon (Should be returned)
                var validMarketing = new Coupon(Guid.NewGuid(), "SPRING2026", "Spring Sale", 15m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(5), 100);
                db.Set<Coupon>().Add(validMarketing);

                // 2. Inactive Marketing Coupon (Should NOT be returned)
                var inactiveMarketing = new Coupon(Guid.NewGuid(), "INACTIVE", "Inactive", 10m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(5), 100);
                inactiveMarketing.Deactivate();
                db.Set<Coupon>().Add(inactiveMarketing);

                // 3. Expired Marketing Coupon (Should NOT be returned)
                var expiredMarketing = new Coupon(Guid.NewGuid(), "EXPIRED", "Expired", 10m, DateTime.UtcNow.AddDays(-5), DateTime.UtcNow.AddDays(5), 100);
                db.Set<Coupon>().Add(expiredMarketing);

                // 4. Compensation Coupon (Should NOT be returned)
                var student = await db.Set<Domain.IdentityManagement.Aggregate.User>().FirstAsync(u => u.Email == "student@example.com");
                var compCoupon = new Coupon(Guid.NewGuid(), student.UserID, "COMPENSATION10", 10m, "Compensation");
                db.Set<Coupon>().Add(compCoupon);

                await db.SaveChangesAsync();

                // Force ExpiredDate in database to be in the past to bypass constructor validation
                await db.Database.ExecuteSqlRawAsync("UPDATE \"Coupons\" SET \"ExpiredDate\" = {0} WHERE \"Code\" = 'EXPIRED'", DateTime.UtcNow.AddDays(-1));
            });

            // Act
            var response = await Client.GetAsync("/api/coupons/available");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<IEnumerable<CouponDTO>>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Data.Should().HaveCount(1);
            result.Data.First().Code.Should().Be("SPRING2026");
        }

        [Fact]
        public async Task AdminEndpoints_StudentAccess_ReturnsForbidden()
        {
            // Arrange
            await Factory.ResetDatabaseAsync();
            await LoginExistingUserAsync("student@example.com", "Password123!");

            var createRequest = new CreateCouponRequest
            {
                Code = "FORBIDDEN",
                Description = "Forbidden Coupon",
                DiscountAmount = 10m,
                StartDate = DateTime.UtcNow.AddDays(1),
                ExpiredDate = DateTime.UtcNow.AddDays(5),
                MaxUsage = 100
            };

            // Act
            var response = await Client.PostAsJsonAsync("/api/admin/coupons", createRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task AdminEndpoints_AnonymousAccess_ReturnsUnauthorized()
        {
            // Arrange
            await Factory.ResetDatabaseAsync();
            // Ensure client is not authorized by resetting headers if any
            Client.DefaultRequestHeaders.Authorization = null;

            var createRequest = new CreateCouponRequest
            {
                Code = "UNAUTHORIZED",
                Description = "Unauthorized Coupon",
                DiscountAmount = 10m,
                StartDate = DateTime.UtcNow.AddDays(1),
                ExpiredDate = DateTime.UtcNow.AddDays(5),
                MaxUsage = 100
            };

            // Act
            var response = await Client.PostAsJsonAsync("/api/admin/coupons", createRequest);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task AdminCouponCrudFlow_Success()
        {
            // Arrange
            await Factory.ResetDatabaseAsync();
            await LoginExistingUserAsync("admin@example.com", "Password123!");

            var createRequest = new CreateCouponRequest
            {
                Code = "WINTER2026",
                Description = "Winter Sale",
                DiscountAmount = 20m,
                StartDate = DateTime.UtcNow.AddDays(1),
                ExpiredDate = DateTime.UtcNow.AddDays(10),
                MaxUsage = 50
            };

            // 1. Create Coupon
            var createResponse = await Client.PostAsJsonAsync("/api/admin/coupons", createRequest);
            createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<Guid>>();
            createResult.Should().NotBeNull();
            createResult!.IsSuccess.Should().BeTrue();
            var couponId = createResult.Data;
            couponId.Should().NotBeEmpty();

            // 2. Get Coupon Details
            var detailsResponse = await Client.GetAsync($"/api/admin/coupons/{couponId}");
            detailsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var detailsResult = await detailsResponse.Content.ReadFromJsonAsync<ApiResponse<CouponDTO>>();
            detailsResult.Should().NotBeNull();
            detailsResult!.Data!.Code.Should().Be("WINTER2026");
            detailsResult.Data.DiscountAmount.Should().Be(20m);
            detailsResult.Data.MaxUsage.Should().Be(50);
            detailsResult.Data.IsActive.Should().BeTrue();

            // 3. Update Coupon
            var updateRequest = new UpdateCouponRequest
            {
                Description = "Updated Winter Sale",
                DiscountAmount = 25m,
                StartDate = DateTime.UtcNow.AddDays(2),
                ExpiredDate = DateTime.UtcNow.AddDays(12),
                MaxUsage = 100
            };

            var updateResponse = await Client.PutAsJsonAsync($"/api/admin/coupons/{couponId}", updateRequest);
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify update in DB
            var detailsResponse2 = await Client.GetAsync($"/api/admin/coupons/{couponId}");
            var detailsResult2 = await detailsResponse2.Content.ReadFromJsonAsync<ApiResponse<CouponDTO>>();
            detailsResult2!.Data!.Description.Should().Be("Updated Winter Sale");
            detailsResult2.Data.DiscountAmount.Should().Be(25m);
            detailsResult2.Data.MaxUsage.Should().Be(100);

            // 4. Deactivate Coupon
            var statusRequest = new UpdateCouponStatusRequest { IsActive = false };
            var statusResponse = await Client.PutAsJsonAsync($"/api/admin/coupons/{couponId}/status", statusRequest);
            statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Verify status in DB
            var detailsResponse3 = await Client.GetAsync($"/api/admin/coupons/{couponId}");
            var detailsResult3 = await detailsResponse3.Content.ReadFromJsonAsync<ApiResponse<CouponDTO>>();
            detailsResult3!.Data!.IsActive.Should().BeFalse();
        }

        [Fact]
        public async Task CouponCode_UniqueConstraint_ShouldThrowConflict()
        {
            // Arrange
            await Factory.ResetDatabaseAsync();
            await LoginExistingUserAsync("admin@example.com", "Password123!");

            // Create first coupon
            var firstRequest = new CreateCouponRequest
            {
                Code = "UNIQUECODE",
                Description = "First",
                DiscountAmount = 10m,
                StartDate = DateTime.UtcNow.AddDays(1),
                ExpiredDate = DateTime.UtcNow.AddDays(5),
                MaxUsage = 100
            };
            var resp1 = await Client.PostAsJsonAsync("/api/admin/coupons", firstRequest);
            resp1.StatusCode.Should().Be(HttpStatusCode.Created);

            // Create second coupon with duplicate code (case-insensitive)
            var duplicateRequest = new CreateCouponRequest
            {
                Code = "UniqueCode",
                Description = "Second",
                DiscountAmount = 15m,
                StartDate = DateTime.UtcNow.AddDays(1),
                ExpiredDate = DateTime.UtcNow.AddDays(5),
                MaxUsage = 100
            };

            // Act
            var resp2 = await Client.PostAsJsonAsync("/api/admin/coupons", duplicateRequest);

            // Assert
            resp2.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        [Fact]
        public async Task AdminListCoupons_FiltersAndPagination_Work()
        {
            // Arrange
            await Factory.ResetDatabaseAsync();
            await LoginExistingUserAsync("admin@example.com", "Password123!");

            await ExecuteDbContextAsync(async db =>
            {
                // Clear existing first
                db.Set<Coupon>().RemoveRange(await db.Set<Coupon>().ToListAsync());
                await db.SaveChangesAsync();

                // Add test coupons
                var c1 = new Coupon(Guid.NewGuid(), "SUMMER10", "Summer Promo 10", 10m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(5), 100);
                var c2 = new Coupon(Guid.NewGuid(), "SUMMER20", "Summer Promo 20", 20m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(5), 100);
                var c3 = new Coupon(Guid.NewGuid(), "WINTER30", "Winter Promo 30", 30m, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(5), 100);
                c3.Deactivate();

                db.Set<Coupon>().AddRange(c1, c2, c3);
                await db.SaveChangesAsync();
            });

            // Filter by search=summer
            var response1 = await Client.GetAsync("/api/admin/coupons?search=summer&pageSize=5");
            response1.StatusCode.Should().Be(HttpStatusCode.OK);
            var result1 = await response1.Content.ReadFromJsonAsync<ApiResponse<PagedResult<CouponDTO>>>();
            result1!.Data!.Items.Should().HaveCount(2);
            result1.Data.Items.All(c => c.Code.Contains("SUMMER")).Should().BeTrue();

            // Filter by isActive=false
            var response2 = await Client.GetAsync("/api/admin/coupons?isActive=false");
            response2.StatusCode.Should().Be(HttpStatusCode.OK);
            var result2 = await response2.Content.ReadFromJsonAsync<ApiResponse<PagedResult<CouponDTO>>>();
            result2!.Data!.Items.Should().HaveCount(1);
            result2.Data.Items.First().Code.Should().Be("WINTER30");
        }

        [Fact]
        public async Task UpdateCoupon_ConcurrencyConflict_ReturnsConflict409()
        {
            // Arrange
            await Factory.ResetDatabaseAsync();
            await LoginExistingUserAsync("admin@example.com", "Password123!");

            // 1. Create Coupon
            var createRequest = new CreateCouponRequest
            {
                Code = "CONCURRENCY",
                Description = "Original",
                DiscountAmount = 10m,
                StartDate = DateTime.UtcNow.AddDays(1),
                ExpiredDate = DateTime.UtcNow.AddDays(5),
                MaxUsage = 100
            };
            var createResponse = await Client.PostAsJsonAsync("/api/admin/coupons", createRequest);
            var createResult = await createResponse.Content.ReadFromJsonAsync<ApiResponse<Guid>>();
            var couponId = createResult!.Data;

            // 2. Send multiple concurrent update requests to the same coupon
            var tasks = new List<Task<System.Net.Http.HttpResponseMessage>>();
            for (int i = 0; i < 10; i++)
            {
                var updateRequest = new UpdateCouponRequest
                {
                    Description = $"Updated by Admin {i}",
                    DiscountAmount = 10m + i,
                    StartDate = DateTime.UtcNow.AddDays(1),
                    ExpiredDate = DateTime.UtcNow.AddDays(5),
                    MaxUsage = 100
                };
                tasks.Add(Client.PutAsJsonAsync($"/api/admin/coupons/{couponId}", updateRequest));
            }

            var responses = await Task.WhenAll(tasks);

            // Assert: at least one update should succeed (200 OK) and at least one should fail with 409 Conflict
            var statuses = responses.Select(r => r.StatusCode).ToList();
            statuses.Should().Contain(HttpStatusCode.OK);
            statuses.Should().Contain(HttpStatusCode.Conflict);
        }
    }
}
