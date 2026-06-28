using Application.Enums;
using Application.Features.Statistics.Queries.GetAnalyticsNormalizedGrowth;
using Application.Results;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.EnrollmentManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Statistics.Queries.GetAnalyticsNormalizedGrowth
{
    public class GetAnalyticsNormalizedGrowthQueryHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<ICourseRepository> _mockCourseRepository;
        private readonly Mock<IEnrollmentRepository> _mockEnrollmentRepository;
        private readonly Mock<IOrderRepository> _mockOrderRepository;
        private readonly GetAnalyticsNormalizedGrowthQueryHandler _handler;

        public GetAnalyticsNormalizedGrowthQueryHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockCourseRepository = new Mock<ICourseRepository>();
            _mockEnrollmentRepository = new Mock<IEnrollmentRepository>();
            _mockOrderRepository = new Mock<IOrderRepository>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IUserRepository>())
                .Returns(_mockUserRepository.Object);

            _mockUnitOfWork
                .Setup(u => u.GetRepository<ICourseRepository>())
                .Returns(_mockCourseRepository.Object);

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IEnrollmentRepository>())
                .Returns(_mockEnrollmentRepository.Object);

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IOrderRepository>())
                .Returns(_mockOrderRepository.Object);

            _handler = new GetAnalyticsNormalizedGrowthQueryHandler(_mockUnitOfWork.Object);
        }

        [Fact]
        public async Task Handle_AllRepositoriesEmpty_ShouldReturnAllZeroSeries()
        {
            // Arrange
            var query = new GetAnalyticsNormalizedGrowthQuery
            {
                Type = AnalyticsGrowthType.User,
                From = DateTime.UtcNow.AddMonths(-1),
                To = DateTime.UtcNow,
                GroupBy = AnalyticGroupDate.Month
            };

            var emptyDict = new Dictionary<string, List<(string Label, decimal Value)>>();

            _mockUserRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", null))
                .ReturnsAsync(emptyDict);

            _mockCourseRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", null, null))
                .ReturnsAsync(emptyDict);

            _mockEnrollmentRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", null, null))
                .ReturnsAsync(emptyDict);

            _mockOrderRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", "All"))
                .ReturnsAsync(emptyDict);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Type.Should().Be(AnalyticsGrowthType.User);
            result.Series.Should().HaveCount(4);

            foreach (var series in result.Series)
            {
                series.Data.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task Handle_ValidDataWithFirstValueZero_ShouldNormalizeToZero()
        {
            // Arrange
            var query = new GetAnalyticsNormalizedGrowthQuery
            {
                Type = AnalyticsGrowthType.User,
                From = DateTime.UtcNow.AddMonths(-1),
                To = DateTime.UtcNow,
                GroupBy = AnalyticGroupDate.Month,
                UserRole = "Student"
            };

            var userData = new Dictionary<string, List<(string Label, decimal Value)>>
            {
                ["Users"] = new List<(string Label, decimal Value)>
                {
                    ("Jan", 10m),
                    ("Feb", 0m) // Feb is alphabetically first, value is 0m
                }
            };

            var emptyDict = new Dictionary<string, List<(string Label, decimal Value)>>();

            _mockUserRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", "Student"))
                .ReturnsAsync(userData);

            _mockCourseRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", null, null))
                .ReturnsAsync(emptyDict);

            _mockEnrollmentRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", null, null))
                .ReturnsAsync(emptyDict);

            _mockOrderRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", "All"))
                .ReturnsAsync(emptyDict);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            var userSeries = result.Series.First(s => s.SeriesName == "Users");

            // Nhãn sắp xếp: Feb, Jan
            userSeries.Data.Select(d => d.Label).Should().Equal("Feb", "Jan");
            // Do firstValue = 0 nên tất cả kết quả chuẩn hóa đều = 0
            userSeries.Data.Select(d => d.Value).Should().Equal(0m, 0m);
        }

        [Fact]
        public async Task Handle_ValidDataWithFirstValueNonZero_ShouldNormalizeToPercentage()
        {
            // Arrange
            var query = new GetAnalyticsNormalizedGrowthQuery
            {
                Type = AnalyticsGrowthType.User,
                From = DateTime.UtcNow.AddMonths(-1),
                To = DateTime.UtcNow,
                GroupBy = AnalyticGroupDate.Month
            };

            var userData = new Dictionary<string, List<(string Label, decimal Value)>>
            {
                ["Users"] = new List<(string Label, decimal Value)>
                {
                    ("Jan", 5m),
                    ("Feb", 15m)
                }
            };

            var courseData = new Dictionary<string, List<(string Label, decimal Value)>>
            {
                ["Courses"] = new List<(string Label, decimal Value)>
                {
                    ("Jan", 10m),
                    ("Mar", 20m)
                }
            };

            var emptyDict = new Dictionary<string, List<(string Label, decimal Value)>>();

            _mockUserRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", null))
                .ReturnsAsync(userData);

            _mockCourseRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", null, null))
                .ReturnsAsync(courseData);

            _mockEnrollmentRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", null, null))
                .ReturnsAsync(emptyDict);

            _mockOrderRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", "All"))
                .ReturnsAsync(emptyDict);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();

            // Nhãn gộp sắp xếp: Feb, Jan, Mar
            var expectedLabels = new[] { "Feb", "Jan", "Mar" };

            var userSeries = result.Series.First(s => s.SeriesName == "Users");
            userSeries.Data.Select(d => d.Label).Should().Equal(expectedLabels);
            // Alphabetically first key in userDict is "Feb" (value 15).
            // "Feb" = 15/15*100 = 100%, "Jan" = 5/15*100 = 33.33%, "Mar" = 0/15*100 = 0%
            userSeries.Data.Select(d => d.Value).Should().Equal(100m, 33.33m, 0m);

            var courseSeries = result.Series.First(s => s.SeriesName == "Courses");
            courseSeries.Data.Select(d => d.Label).Should().Equal(expectedLabels);
            // Alphabetically first key in courseDict is "Jan" (value 10).
            // "Feb" = 0/10*100 = 0%, "Jan" = 10/10*100 = 100%, "Mar" = 20/10*100 = 200%
            courseSeries.Data.Select(d => d.Value).Should().Equal(0m, 100m, 200m);
        }
    }
}
