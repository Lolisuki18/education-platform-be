using Application;
using Application.Features.Academic.Queries.GetGrades;
using Application.Features.Academic.Queries.GetSubjects;
using Application.Features.Statistics.Queries.GetSummaryStatistic;
using Application.Features.Statistics.Queries.GetSummaryStatistics;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.AcademicManagement.Aggregate;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Statistics.Queries.GetSummaryStatistics
{
    public class GetSummaryStatisticsQueryHandlerTests
    {
        private readonly IMapper _mapper;
        private readonly Mock<IApplicationDBContext> _mockContext;
        private readonly Mock<IMediator> _mockMediator;
        private readonly GetSummaryStatisticsQueryHandler _handler;

        public GetSummaryStatisticsQueryHandlerTests()
        {
            var mockLoggerFactory = new Mock<ILoggerFactory>();
            var mockLogger = new Mock<ILogger>();
            mockLoggerFactory
                .Setup(f => f.CreateLogger(It.IsAny<string>()))
                .Returns(mockLogger.Object);

            var mapperConfig = new MapperConfiguration(cfg =>
            {
                cfg.AddMaps(typeof(ApplicationDI).Assembly);
            }, mockLoggerFactory.Object);
            _mapper = mapperConfig.CreateMapper();

            _mockContext = new Mock<IApplicationDBContext>();
            _mockMediator = new Mock<IMediator>();

            _handler = new GetSummaryStatisticsQueryHandler(
                _mockContext.Object,
                _mapper,
                _mockMediator.Object);
        }

        [Fact]
        public async Task Handle_DefaultRequest_ShouldReturnGradesSubjectsAndMediatedSummary()
        {
            // Arrange
            var grade = new Grade(Guid.NewGuid(), "Grade 12");
            var subject = new Subject(Guid.NewGuid(), "CHEM", "Chemistry", grade.GradeID);

            var gradesList = new List<Grade> { grade };
            var subjectsList = new List<Subject> { subject };

            _mockContext.Setup(c => c.Grades).Returns(DbSetMockHelper.CreateMockDbSet(gradesList).Object);
            _mockContext.Setup(c => c.Subjects).Returns(DbSetMockHelper.CreateMockDbSet(subjectsList).Object);

            var expectedSummary = new SummaryStatisticDTO
            {
                User = new SummaryUserDTO { Total = 100 }
            };

            _mockMediator
                .Setup(m => m.Send(It.IsAny<GetSummaryStatisticQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedSummary);

            var query = new GetSummaryStatisticsQuery();

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Grades.Should().HaveCount(1);
            result.Grades.First().Name.Should().Be("Grade 12");

            result.Subjects.Should().HaveCount(1);
            result.Subjects.First().Name.Should().Be("Chemistry");

            result.Summary.Should().NotBeNull();
            result.Summary.User.Total.Should().Be(100);

            // Xác thực Mediator đã được gọi với khoảng thời gian mặc định (Min và Max Value)
            _mockMediator.Verify(m => m.Send(It.Is<GetSummaryStatisticQuery>(q =>
                q.From == DateTime.MinValue &&
                q.To == DateTime.MaxValue
            ), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_WithDatesRequest_ShouldForwardDatesToMediator()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-10);
            var toDate = DateTime.UtcNow.AddDays(-2);

            _mockContext.Setup(c => c.Grades).Returns(DbSetMockHelper.CreateMockDbSet(new List<Grade>()).Object);
            _mockContext.Setup(c => c.Subjects).Returns(DbSetMockHelper.CreateMockDbSet(new List<Subject>()).Object);

            var expectedSummary = new SummaryStatisticDTO();
            _mockMediator
                .Setup(m => m.Send(It.IsAny<GetSummaryStatisticQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedSummary);

            var query = new GetSummaryStatisticsQuery { From = fromDate, To = toDate };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();

            // Xác thực Mediator nhận đúng khoảng thời gian truyền từ Request
            _mockMediator.Verify(m => m.Send(It.Is<GetSummaryStatisticQuery>(q =>
                q.From == fromDate &&
                q.To == toDate
            ), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
