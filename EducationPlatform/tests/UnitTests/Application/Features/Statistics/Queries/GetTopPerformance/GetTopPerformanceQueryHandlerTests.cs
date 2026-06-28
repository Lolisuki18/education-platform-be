using Application.Features.Statistics.Queries.GetTopPerformance;
using Application.Results;
using Domain.Common.Interfaces;
using Domain.EnrollmentManagement.Aggregate;
using Domain.OrderManagement.Aggregate;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Statistics.Queries.GetTopPerformance
{
    public class GetTopPerformanceQueryHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IEnrollmentRepository> _mockEnrollmentRepository;
        private readonly Mock<IOrderRepository> _mockOrderRepository;
        private readonly GetTopPerformanceQueryHandler _handler;

        public GetTopPerformanceQueryHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockEnrollmentRepository = new Mock<IEnrollmentRepository>();
            _mockOrderRepository = new Mock<IOrderRepository>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IEnrollmentRepository>())
                .Returns(_mockEnrollmentRepository.Object);

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IOrderRepository>())
                .Returns(_mockOrderRepository.Object);

            _handler = new GetTopPerformanceQueryHandler(_mockUnitOfWork.Object);
        }

        [Fact]
        public async Task Handle_ValidRequest_ShouldReturnMappedTopPerformanceLists()
        {
            // Arrange
            var fromDate = DateTime.UtcNow.AddDays(-30);
            var toDate = DateTime.UtcNow;
            var gradeId = Guid.NewGuid();
            var subjectId = Guid.NewGuid();
            var top = 5;

            var query = new GetTopPerformanceQuery
            {
                From = fromDate,
                To = toDate,
                GradeId = gradeId,
                SubjectId = subjectId,
                Top = top
            };

            // 1. Mock Enrollment Repo returns
            var courseId = Guid.NewGuid();
            var subjectId2 = Guid.NewGuid();
            var gradeId2 = Guid.NewGuid();

            _mockEnrollmentRepository
                .Setup(r => r.GetTopCoursesByEnrollment(fromDate, toDate, gradeId, subjectId, top))
                .ReturnsAsync(new List<(Guid CourseId, string CourseName, decimal EnrollmentCount)>
                {
                    (courseId, "Course A", 10m)
                });

            _mockEnrollmentRepository
                .Setup(r => r.GetTopSubjectsByEnrollment(fromDate, toDate, gradeId, top))
                .ReturnsAsync(new List<(Guid SubjectId, string SubjectName, decimal EnrollmentCount)>
                {
                    (subjectId2, "Subject X", 15m)
                });

            _mockEnrollmentRepository
                .Setup(r => r.GetTopGradesByEnrollment(fromDate, toDate, subjectId, top))
                .ReturnsAsync(new List<(Guid GradeId, string GradeName, decimal EnrollmentCount)>
                {
                    (gradeId2, "Grade Y", 20m)
                });

            // 2. Mock Order Repo returns
            _mockOrderRepository
                .Setup(r => r.GetTopCoursesByRevenue(fromDate, toDate, gradeId, subjectId, top))
                .ReturnsAsync(new List<(Guid CourseId, string CourseName, decimal Revenue)>
                {
                    (courseId, "Course A", 1000m)
                });

            _mockOrderRepository
                .Setup(r => r.GetTopSubjectsByRevenue(fromDate, toDate, gradeId, top))
                .ReturnsAsync(new List<(Guid SubjectId, string SubjectName, decimal Revenue)>
                {
                    (subjectId2, "Subject X", 1500m)
                });

            _mockOrderRepository
                .Setup(r => r.GetTopGradesByRevenue(fromDate, toDate, subjectId, top))
                .ReturnsAsync(new List<(Guid GradeId, string GradeName, decimal Revenue)>
                {
                    (gradeId2, "Grade Y", 2000m)
                });

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();

            // Verification of mapping
            result.CoursesByEnrollment.Should().HaveCount(1);
            result.CoursesByEnrollment.First().CourseId.Should().Be(courseId);
            result.CoursesByEnrollment.First().CourseName.Should().Be("Course A");
            result.CoursesByEnrollment.First().EnrollmentCount.Should().Be(10m);

            result.SubjectsByEnrollment.Should().HaveCount(1);
            result.SubjectsByEnrollment.First().SubjectId.Should().Be(subjectId2);
            result.SubjectsByEnrollment.First().SubjectName.Should().Be("Subject X");
            result.SubjectsByEnrollment.First().EnrollmentCount.Should().Be(15m);

            result.GradesByEnrollment.Should().HaveCount(1);
            result.GradesByEnrollment.First().GradeId.Should().Be(gradeId2);
            result.GradesByEnrollment.First().GradeName.Should().Be("Grade Y");
            result.GradesByEnrollment.First().EnrollmentCount.Should().Be(20m);

            result.CoursesByRevenue.Should().HaveCount(1);
            result.CoursesByRevenue.First().CourseId.Should().Be(courseId);
            result.CoursesByRevenue.First().CourseName.Should().Be("Course A");
            result.CoursesByRevenue.First().Revenue.Should().Be(1000m);

            result.SubjectsByRevenue.Should().HaveCount(1);
            result.SubjectsByRevenue.First().SubjectId.Should().Be(subjectId2);
            result.SubjectsByRevenue.First().SubjectName.Should().Be("Subject X");
            result.SubjectsByRevenue.First().Revenue.Should().Be(1500m);

            result.GradesByRevenue.Should().HaveCount(1);
            result.GradesByRevenue.First().GradeId.Should().Be(gradeId2);
            result.GradesByRevenue.First().GradeName.Should().Be("Grade Y");
            result.GradesByRevenue.First().Revenue.Should().Be(2000m);

            // Verify all repository calls were made with the exact parameters
            _mockEnrollmentRepository.Verify(r => r.GetTopCoursesByEnrollment(fromDate, toDate, gradeId, subjectId, top), Times.Once);
            _mockEnrollmentRepository.Verify(r => r.GetTopSubjectsByEnrollment(fromDate, toDate, gradeId, top), Times.Once);
            _mockEnrollmentRepository.Verify(r => r.GetTopGradesByEnrollment(fromDate, toDate, subjectId, top), Times.Once);

            _mockOrderRepository.Verify(r => r.GetTopCoursesByRevenue(fromDate, toDate, gradeId, subjectId, top), Times.Once);
            _mockOrderRepository.Verify(r => r.GetTopSubjectsByRevenue(fromDate, toDate, gradeId, top), Times.Once);
            _mockOrderRepository.Verify(r => r.GetTopGradesByRevenue(fromDate, toDate, subjectId, top), Times.Once);
        }
    }
}
