using Application.BusinessException;
using Application.Features.Enrollments.Queries;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.EnrollmentManagement.Aggregate;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Enrollments.Queries
{
    public class GetStudentEnrollmentsQueryTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IEnrollmentRepository> _mockEnrollmentRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly GetStudentEnrollmentsQueryHandler _handler;

        public GetStudentEnrollmentsQueryTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockEnrollmentRepository = new Mock<IEnrollmentRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockCurrentUser = new Mock<ICurrentUser>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IEnrollmentRepository>())
                .Returns(_mockEnrollmentRepository.Object);

            _handler = new GetStudentEnrollmentsQueryHandler(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockCurrentUser.Object);
        }

        [Fact]
        public async Task Handle_UserNotAuthenticated_ShouldThrowAuthenticateException()
        {
            // Arrange
            _mockCurrentUser.Setup(u => u.Id).Returns((Guid?)null);
            var query = new GetStudentEnrollmentsQuery();

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("User must be authenticated.");
        }

        [Fact]
        public async Task Handle_ValidRequest_ShouldReturnMappedStudentEnrollments()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);

            var enrollment1 = new Enrollment(Guid.NewGuid(), studentId, Guid.NewGuid(), DateTime.UtcNow);
            var enrollment2 = new Enrollment(Guid.NewGuid(), studentId, Guid.NewGuid(), DateTime.UtcNow);
            var enrollmentsList = new List<Enrollment> { enrollment1, enrollment2 };

            _mockEnrollmentRepository
                .Setup(r => r.GetStudentEnrollments(studentId))
                .ReturnsAsync(enrollmentsList);

            var expectedDtos = new List<EnrollmentDTO>
            {
                new EnrollmentDTO { EnrollmentID = enrollment1.EnrollmentID, StudentID = studentId },
                new EnrollmentDTO { EnrollmentID = enrollment2.EnrollmentID, StudentID = studentId }
            };

            _mockMapper
                .Setup(m => m.Map<IEnumerable<EnrollmentDTO>>(enrollmentsList))
                .Returns(expectedDtos);

            var query = new GetStudentEnrollmentsQuery();

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedDtos);
            _mockEnrollmentRepository.Verify(r => r.GetStudentEnrollments(studentId), Times.Once);
        }
    }
}
