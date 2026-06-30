using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using API.Models.Common;
using Application.Features.Statistics.Queries.GetSummaryStatistics;
using Application.Results;
using FluentAssertions;
using IntegrationTests;
using Xunit;

namespace FunctionalTests
{
    public class AdminDashboardFlowTests : IntegrationTestBase
    {
        public AdminDashboardFlowTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task AdminDashboardAndStatisticsFlow_ShouldSucceed()
        {
            // Login as Admin
            var adminClient = await CreateAuthenticatedClientAsync("admin@example.com", "Password123!");

            // 1. Get Summary Stats
            var summaryResponse = await adminClient.GetAsync("/api/statistics/summary?StartDate=2026-06-01&EndDate=2026-06-30");
            summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var summaryResult = await summaryResponse.Content.ReadFromJsonAsync<ApiResponse<SummaryStatisticsResult>>();
            summaryResult.Should().NotBeNull();
            summaryResult!.IsSuccess.Should().BeTrue();
            summaryResult.Data.Should().NotBeNull();
            summaryResult.Data!.Grades.Should().NotBeNull();
            summaryResult.Data.Subjects.Should().NotBeNull();

            // 2. Get Analytics Growth
            var growthResponse = await adminClient.GetAsync("/api/statistics/analytics/growth?Type=User&GroupBy=Month");
            growthResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var growthResult = await growthResponse.Content.ReadFromJsonAsync<ApiResponse<AnalyticsGrowthDTO>>();
            growthResult.Should().NotBeNull();
            growthResult!.IsSuccess.Should().BeTrue();
            growthResult.Data.Should().NotBeNull();
            growthResult.Data!.Series.Should().NotBeNull();

            // 3. Get Analytics Demand and Supply
            var demandResponse = await adminClient.GetAsync("/api/statistics/analytics/demand-supply");
            demandResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var demandResult = await demandResponse.Content.ReadFromJsonAsync<ApiResponse<AnalyticsGrowthDTO>>();
            demandResult.Should().NotBeNull();
            demandResult!.IsSuccess.Should().BeTrue();
            demandResult.Data.Should().NotBeNull();

            // 4. Get Analytics Normalized Growth
            var normalizedResponse = await adminClient.GetAsync("/api/statistics/analytics/normalized-growth");
            normalizedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var normalizedResult = await normalizedResponse.Content.ReadFromJsonAsync<ApiResponse<AnalyticsGrowthDTO>>();
            normalizedResult.Should().NotBeNull();
            normalizedResult!.IsSuccess.Should().BeTrue();

            // 5. Get Top Performance
            var topPerfResponse = await adminClient.GetAsync("/api/statistics/analytics/top-performance");
            topPerfResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var topPerfResult = await topPerfResponse.Content.ReadFromJsonAsync<ApiResponse<TopPerformanceDTO>>();
            topPerfResult.Should().NotBeNull();
            topPerfResult!.IsSuccess.Should().BeTrue();
            topPerfResult.Data.Should().NotBeNull();
        }
    }
}
