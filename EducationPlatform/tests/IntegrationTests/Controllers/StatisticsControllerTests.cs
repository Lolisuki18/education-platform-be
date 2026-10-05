using System.Net;
using System.Threading.Tasks;
using API.Models.Common;
using Application.Results;
using FluentAssertions;
using System.Net.Http.Json;
using Xunit;
using Application.Features.Statistics.Queries.GetSummaryStatistics;

namespace IntegrationTests.Controllers
{
    public class StatisticsControllerTests : IntegrationTestBase
    {
        public StatisticsControllerTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task GetSummary_AsAdmin_ReturnsStatistics()
        {
            // Arrange
            await LoginExistingUserAsync("admin@example.com", "Password123!");

            // Act
            var response = await Client.GetAsync("/api/statistics/summary");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<SummaryStatisticsResult>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
            result.Data!.Grades.Should().NotBeNull();
            result.Data.Subjects.Should().NotBeNull();
        }

        [Fact]
        public async Task GetAnalyticsGrowth_AsAdmin_ReturnsGrowthData()
        {
            // Arrange
            await LoginExistingUserAsync("admin@example.com", "Password123!");

            // Act
            var response = await Client.GetAsync("/api/statistics/analytics/growth?Type=User&GroupBy=Month");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var result = await response.Content.ReadFromJsonAsync<ApiResponse<AnalyticsGrowthDTO>>();
            result.Should().NotBeNull();
            result!.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task GetSummary_AsStudent_ReturnsForbidden()
        {
            // Arrange
            await LoginExistingUserAsync("student@example.com", "Password123!");

            // Act
            var response = await Client.GetAsync("/api/statistics/summary");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetSummary_AsTeacher_ReturnsForbidden()
        {
            // Arrange
            await LoginExistingUserAsync("teacher@example.com", "Password123!");

            // Act
            var response = await Client.GetAsync("/api/statistics/summary");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task GetSummary_WhenUnauthenticated_ReturnsUnauthorized()
        {
            // Arrange (No authentication token set)

            // Act
            var response = await Client.GetAsync("/api/statistics/summary");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }
}
