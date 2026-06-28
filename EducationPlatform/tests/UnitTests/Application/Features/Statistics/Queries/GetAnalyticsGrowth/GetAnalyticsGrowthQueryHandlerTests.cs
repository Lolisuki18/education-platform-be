using Application.Enums;
using Application.Features.Statistics.Queries.GetAnalyticsGrowth;
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

namespace UnitTests.Application.Features.Statistics.Queries.GetAnalyticsGrowth
{
    public class GetAnalyticsGrowthQueryHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IUserRepository> _mockUserRepository;
        private readonly Mock<ICourseRepository> _mockCourseRepository;
        private readonly Mock<IEnrollmentRepository> _mockEnrollmentRepository;
        private readonly Mock<IOrderRepository> _mockOrderRepository;
        private readonly GetAnalyticsGrowthQueryHandler _handler;

        public GetAnalyticsGrowthQueryHandlerTests()
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

            _handler = new GetAnalyticsGrowthQueryHandler(_mockUnitOfWork.Object);
        }

        [Fact]
        public async Task Handle_UserType_ShouldCallUserRepo()
        {
            // Arrange
            var query = new GetAnalyticsGrowthQuery
            {
                Type = AnalyticsGrowthType.User,
                From = DateTime.UtcNow.AddMonths(-1),
                To = DateTime.UtcNow,
                GroupBy = AnalyticGroupDate.Month,
                UserRole = "Student"
            };

            var expectedData = new Dictionary<string, List<(string Label, decimal Value)>>
            {
                ["Users"] = new List<(string Label, decimal Value)> { ("Jan", 15m) }
            };

            _mockUserRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", "Student"))
                .ReturnsAsync(expectedData);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Type.Should().Be(AnalyticsGrowthType.User);
            result.Series.Should().HaveCount(1);
            result.Series[0].SeriesName.Should().Be("Main");
            result.Series[0].Data.Should().HaveCount(1);
            result.Series[0].Data[0].Label.Should().Be("Jan");
            result.Series[0].Data[0].Value.Should().Be(15m);
        }

        [Fact]
        public async Task Handle_CourseType_ShouldCallCourseRepo()
        {
            // Arrange
            var gradeId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var query = new GetAnalyticsGrowthQuery
            {
                Type = AnalyticsGrowthType.Course,
                From = DateTime.UtcNow.AddMonths(-1),
                To = DateTime.UtcNow,
                GroupBy = AnalyticGroupDate.Month,
                CourseGradeId = gradeId,
                CourseSubjectId = subjectId
            };

            var expectedData = new Dictionary<string, List<(string Label, decimal Value)>>
            {
                ["Courses"] = new List<(string Label, decimal Value)> { ("Jan", 5m) }
            };

            _mockCourseRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", gradeId, subjectId))
                .ReturnsAsync(expectedData);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Type.Should().Be(AnalyticsGrowthType.Course);
            _mockCourseRepository.Verify(r => r.AnalyticsGrowth(query.From, query.To, "Month", gradeId, subjectId), Times.Once);
        }

        [Fact]
        public async Task Handle_EnrollmentType_ShouldCallEnrollmentRepo()
        {
            // Arrange
            var gradeId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var query = new GetAnalyticsGrowthQuery
            {
                Type = AnalyticsGrowthType.Enrollment,
                From = DateTime.UtcNow.AddMonths(-1),
                To = DateTime.UtcNow,
                GroupBy = AnalyticGroupDate.Month,
                EnrollmentGradeId = gradeId,
                EnrollmentSubjectId = subjectId
            };

            var expectedData = new Dictionary<string, List<(string Label, decimal Value)>>
            {
                ["Enrollments"] = new List<(string Label, decimal Value)> { ("Jan", 25m) }
            };

            _mockEnrollmentRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", gradeId, subjectId))
                .ReturnsAsync(expectedData);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Type.Should().Be(AnalyticsGrowthType.Enrollment);
            _mockEnrollmentRepository.Verify(r => r.AnalyticsGrowth(query.From, query.To, "Month", gradeId, subjectId), Times.Once);
        }

        [Fact]
        public async Task Handle_RevenueType_ShouldCallOrderRepo()
        {
            // Arrange
            var query = new GetAnalyticsGrowthQuery
            {
                Type = AnalyticsGrowthType.Revenue,
                From = DateTime.UtcNow.AddMonths(-1),
                To = DateTime.UtcNow,
                GroupBy = AnalyticGroupDate.Month,
                RevenueType = AnalyticRevenueType.Commission
            };

            var expectedData = new Dictionary<string, List<(string Label, decimal Value)>>
            {
                ["Revenue"] = new List<(string Label, decimal Value)> { ("Jan", 150m) }
            };

            _mockOrderRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", "Commission"))
                .ReturnsAsync(expectedData);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Type.Should().Be(AnalyticsGrowthType.Revenue);
            _mockOrderRepository.Verify(r => r.AnalyticsGrowth(query.From, query.To, "Month", "Commission"), Times.Once);
        }

        [Fact]
        public async Task Handle_WithComparisonRanges_ShouldRunRangesAndAlignLabels()
        {
            // Arrange
            var fromMain = DateTime.UtcNow.AddMonths(-1);
            var toMain = DateTime.UtcNow;
            var fromCompare = DateTime.UtcNow.AddMonths(-13);
            var toCompare = DateTime.UtcNow.AddMonths(-12);

            var query = new GetAnalyticsGrowthQuery
            {
                Type = AnalyticsGrowthType.User,
                From = fromMain,
                To = toMain,
                GroupBy = AnalyticGroupDate.Month,
                ComparisonRanges = new List<ComparisonRangeDTO>
                {
                    new ComparisonRangeDTO { From = fromCompare, To = toCompare, Label = "Last Year" }
                }
            };

            var mainData = new Dictionary<string, List<(string Label, decimal Value)>>
            {
                ["Users"] = new List<(string Label, decimal Value)> { ("Jan", 10m) }
            };

            var compareData = new Dictionary<string, List<(string Label, decimal Value)>>
            {
                ["Users"] = new List<(string Label, decimal Value)>
                {
                    ("Jan", 5m),
                    ("Feb", 15m)
                }
            };

            _mockUserRepository
                .Setup(r => r.AnalyticsGrowth(fromMain, toMain, "Month", null))
                .ReturnsAsync(mainData);

            _mockUserRepository
                .Setup(r => r.AnalyticsGrowth(fromCompare, toCompare, "Month", null))
                .ReturnsAsync(compareData);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Series.Should().HaveCount(2);

            var mainSeries = result.Series.First(s => s.SeriesName == "Main");
            var compareSeries = result.Series.First(s => s.SeriesName == "Last Year");

            // Nhãn sắp xếp: Feb, Jan
            var expectedLabels = new[] { "Feb", "Jan" };

            mainSeries.Data.Select(d => d.Label).Should().Equal(expectedLabels);
            mainSeries.Data.Select(d => d.Value).Should().Equal(0m, 10m);

            compareSeries.Data.Select(d => d.Label).Should().Equal(expectedLabels);
            compareSeries.Data.Select(d => d.Value).Should().Equal(15m, 5m);
        }
    }
}
