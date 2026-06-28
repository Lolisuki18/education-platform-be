using Application.Enums;
using Application.Features.Statistics.Queries.GetAnalyticsDemandAndSupply;
using Application.Results;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.EnrollmentManagement.Aggregate;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Statistics.Queries.GetAnalyticsDemandAndSupply
{
    public class GetAnalyticsDemandAndSupplyQueryHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICourseRepository> _mockCourseRepository;
        private readonly Mock<IEnrollmentRepository> _mockEnrollmentRepository;
        private readonly GetAnalyticsDemandAndSupplyQueryHandler _handler;

        public GetAnalyticsDemandAndSupplyQueryHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCourseRepository = new Mock<ICourseRepository>();
            _mockEnrollmentRepository = new Mock<IEnrollmentRepository>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<ICourseRepository>())
                .Returns(_mockCourseRepository.Object);

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IEnrollmentRepository>())
                .Returns(_mockEnrollmentRepository.Object);

            _handler = new GetAnalyticsDemandAndSupplyQueryHandler(_mockUnitOfWork.Object);
        }

        [Fact]
        public async Task Handle_BothDataEmpty_ShouldReturnEmptyDataSeries()
        {
            // Arrange
            var query = new GetAnalyticsDemandAndSupplyQuery
            {
                From = DateTime.UtcNow.AddMonths(-1),
                To = DateTime.UtcNow,
                GroupBy = AnalyticGroupDate.Month
            };

            _mockCourseRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", null, null))
                .ReturnsAsync(new Dictionary<string, List<(string Label, decimal Value)>>());

            _mockEnrollmentRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", null, null))
                .ReturnsAsync(new Dictionary<string, List<(string Label, decimal Value)>>());

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Type.Should().Be(AnalyticsGrowthType.Enrollment);
            result.Series.Should().HaveCount(2);

            var supplySeries = result.Series.First(s => s.SeriesName == "Supply (Courses)");
            var demandSeries = result.Series.First(s => s.SeriesName == "Demand (Enrollments)");

            supplySeries.Data.Should().BeEmpty();
            demandSeries.Data.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_OnlyCourseDataExists_ShouldReturnCorrectDataAndZeroForEnrollments()
        {
            // Arrange
            var query = new GetAnalyticsDemandAndSupplyQuery
            {
                From = DateTime.UtcNow.AddMonths(-2),
                To = DateTime.UtcNow,
                GroupBy = AnalyticGroupDate.Month,
                CourseGradeId = Guid.NewGuid(),
                CourseSubjectId = Guid.NewGuid()
            };

            var courseData = new Dictionary<string, List<(string Label, decimal Value)>>
            {
                ["Courses"] = new List<(string Label, decimal Value)>
                {
                    ("Jan", 5m),
                    ("Feb", 10m)
                }
            };

            _mockCourseRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", query.CourseGradeId, query.CourseSubjectId))
                .ReturnsAsync(courseData);

            _mockEnrollmentRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", null, null))
                .ReturnsAsync(new Dictionary<string, List<(string Label, decimal Value)>>());

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Series.Should().HaveCount(2);

            var supplySeries = result.Series.First(s => s.SeriesName == "Supply (Courses)");
            var demandSeries = result.Series.First(s => s.SeriesName == "Demand (Enrollments)");

            // Nhãn phải sắp xếp theo bảng chữ cái: Feb, Jan
            supplySeries.Data.Select(d => d.Label).Should().Equal("Feb", "Jan");
            supplySeries.Data.Select(d => d.Value).Should().Equal(10m, 5m);

            demandSeries.Data.Select(d => d.Label).Should().Equal("Feb", "Jan");
            demandSeries.Data.Select(d => d.Value).Should().Equal(0m, 0m);
        }

        [Fact]
        public async Task Handle_OnlyEnrollmentDataExists_ShouldReturnCorrectDataAndZeroForCourses()
        {
            // Arrange
            var query = new GetAnalyticsDemandAndSupplyQuery
            {
                From = DateTime.UtcNow.AddMonths(-2),
                To = DateTime.UtcNow,
                GroupBy = AnalyticGroupDate.Month,
                EnrollmentGradeId = Guid.NewGuid(),
                EnrollmentSubjectId = Guid.NewGuid()
            };

            var enrollmentData = new Dictionary<string, List<(string Label, decimal Value)>>
            {
                ["Enrollments"] = new List<(string Label, decimal Value)>
                {
                    ("Jan", 20m),
                    ("Mar", 30m)
                }
            };

            _mockCourseRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", null, null))
                .ReturnsAsync(new Dictionary<string, List<(string Label, decimal Value)>>());

            _mockEnrollmentRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", query.EnrollmentGradeId, query.EnrollmentSubjectId))
                .ReturnsAsync(enrollmentData);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();

            var supplySeries = result.Series.First(s => s.SeriesName == "Supply (Courses)");
            var demandSeries = result.Series.First(s => s.SeriesName == "Demand (Enrollments)");

            // Nhãn sắp xếp: Jan, Mar
            supplySeries.Data.Select(d => d.Label).Should().Equal("Jan", "Mar");
            supplySeries.Data.Select(d => d.Value).Should().Equal(0m, 0m);

            demandSeries.Data.Select(d => d.Label).Should().Equal("Jan", "Mar");
            demandSeries.Data.Select(d => d.Value).Should().Equal(20m, 30m);
        }

        [Fact]
        public async Task Handle_BothDataExists_ShouldMergeAndAlignLabelsCorrectly()
        {
            // Arrange
            var query = new GetAnalyticsDemandAndSupplyQuery
            {
                From = DateTime.UtcNow.AddMonths(-3),
                To = DateTime.UtcNow,
                GroupBy = AnalyticGroupDate.Month
            };

            var courseData = new Dictionary<string, List<(string Label, decimal Value)>>
            {
                ["Courses"] = new List<(string Label, decimal Value)>
                {
                    ("Jan", 5m),
                    ("Feb", 10m)
                }
            };

            var enrollmentData = new Dictionary<string, List<(string Label, decimal Value)>>
            {
                ["Enrollments"] = new List<(string Label, decimal Value)>
                {
                    ("Jan", 20m),
                    ("Mar", 30m)
                }
            };

            _mockCourseRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", null, null))
                .ReturnsAsync(courseData);

            _mockEnrollmentRepository
                .Setup(r => r.AnalyticsGrowth(query.From, query.To, "Month", null, null))
                .ReturnsAsync(enrollmentData);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();

            var supplySeries = result.Series.First(s => s.SeriesName == "Supply (Courses)");
            var demandSeries = result.Series.First(s => s.SeriesName == "Demand (Enrollments)");

            // Nhãn gộp và sắp xếp: Feb, Jan, Mar
            var expectedLabels = new[] { "Feb", "Jan", "Mar" };

            supplySeries.Data.Select(d => d.Label).Should().Equal(expectedLabels);
            supplySeries.Data.Select(d => d.Value).Should().Equal(10m, 5m, 0m);

            demandSeries.Data.Select(d => d.Label).Should().Equal(expectedLabels);
            demandSeries.Data.Select(d => d.Value).Should().Equal(0m, 20m, 30m);
        }
    }
}
