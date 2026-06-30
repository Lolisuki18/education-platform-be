using Application.BusinessException;
using Application.Features.Complaints.Commands.CreateComplaint;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.EnrollmentManagement.Aggregate;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Complaints.Commands.CreateComplaint
{
    public class CreateComplaintCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IEnrollmentRepository> _mockEnrollmentRepository;
        private readonly Mock<IComplaintRepository> _mockComplaintRepository;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly Mock<IStorageService> _mockStorageService;
        private readonly CreateComplaintCommandHandler _handler;

        public CreateComplaintCommandHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockEnrollmentRepository = new Mock<IEnrollmentRepository>();
            _mockComplaintRepository = new Mock<IComplaintRepository>();
            _mockCurrentUser = new Mock<ICurrentUser>();
            _mockStorageService = new Mock<IStorageService>();

            // Setup UnitOfWork to return mocks for repositories
            _mockUnitOfWork
                .Setup(u => u.GetRepository<IEnrollmentRepository>())
                .Returns(_mockEnrollmentRepository.Object);

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IComplaintRepository>())
                .Returns(_mockComplaintRepository.Object);

            _handler = new CreateComplaintCommandHandler(
                _mockUnitOfWork.Object,
                _mockCurrentUser.Object,
                _mockStorageService.Object);
        }

        [Fact]
        public async Task Handle_UserNotAuthenticated_ShouldThrowAuthenticateException()
        {
            // Arrange
            _mockCurrentUser.Setup(u => u.Id).Returns((Guid?)null);
            var command = new CreateComplaintCommand { CourseID = Guid.NewGuid(), Reason = "Test Reason" };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("User must be authenticated.");
        }

        [Fact]
        public async Task Handle_UserNotEnrolledInCourse_ShouldThrowConflictException()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            var targetCourseId = Guid.NewGuid();
            var command = new CreateComplaintCommand { CourseID = targetCourseId, Reason = "Test Reason" };

            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);

            // Giả lập student enroll ở khóa học khác, không phải khóa học muốn khiếu nại
            var otherCourseEnrollment = new Enrollment(Guid.NewGuid(), studentId, Guid.NewGuid(), DateTime.UtcNow);
            var enrollments = new List<Enrollment> { otherCourseEnrollment };

            _mockEnrollmentRepository
                .Setup(r => r.GetStudentEnrollments(studentId))
                .ReturnsAsync(enrollments);

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<Conflict>()
                .WithMessage("You can only submit complaints for courses you have enrolled in.");
        }

        [Fact]
        public async Task Handle_ValidRequestWithoutFile_ShouldCreateComplaintAndCommitSuccessfully()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            var command = new CreateComplaintCommand { CourseID = courseId, Reason = "Valid Reason" };

            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);

            var enrollment = new Enrollment(Guid.NewGuid(), studentId, courseId, DateTime.UtcNow);
            var enrollments = new List<Enrollment> { enrollment };

            _mockEnrollmentRepository
                .Setup(r => r.GetStudentEnrollments(studentId))
                .ReturnsAsync(enrollments);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeEmpty();

            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _mockComplaintRepository.Verify(r => r.CreateComplaint(It.Is<Complaint>(c =>
                c.CourseID == courseId &&
                c.StudentID == studentId &&
                c.Reason == "Valid Reason" &&
                c.EvidenceImagePath == null)), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(studentId.ToString()), Times.Once);
        }

        [Fact]
        public async Task Handle_ValidRequestWithFile_ShouldSaveFileCreateComplaintAndCommitSuccessfully()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            using var dummyStream = new MemoryStream();
            var command = new CreateComplaintCommand
            {
                CourseID = courseId,
                Reason = "Valid Reason with file",
                EvidenceFileStream = dummyStream,
                EvidenceFileExtension = ".png"
            };

            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);

            var enrollment = new Enrollment(Guid.NewGuid(), studentId, courseId, DateTime.UtcNow);
            var enrollments = new List<Enrollment> { enrollment };

            _mockEnrollmentRepository
                .Setup(r => r.GetStudentEnrollments(studentId))
                .ReturnsAsync(enrollments);

            // Giả lập lưu file thành công
            _mockStorageService
                .Setup(s => s.SaveAsync(dummyStream, "png", It.IsAny<CancellationToken>()))
                .ReturnsAsync("evidence/image123.png");

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().NotBeEmpty();

            _mockStorageService.Verify(s => s.SaveAsync(dummyStream, "png", It.IsAny<CancellationToken>()), Times.Once);
            _mockComplaintRepository.Verify(r => r.CreateComplaint(It.Is<Complaint>(c =>
                c.CourseID == courseId &&
                c.StudentID == studentId &&
                c.Reason == "Valid Reason with file" &&
                c.EvidenceImagePath == "evidence/image123.png")), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(studentId.ToString()), Times.Once);
        }

        [Fact]
        public async Task Handle_ExceptionDuringCommitWithFile_ShouldDeleteSavedFileAndRethrow()
        {
            // Arrange
            var studentId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            using var dummyStream = new MemoryStream();
            var command = new CreateComplaintCommand
            {
                CourseID = courseId,
                Reason = "Valid Reason with file to fail",
                EvidenceFileStream = dummyStream,
                EvidenceFileExtension = ".jpg"
            };

            _mockCurrentUser.Setup(u => u.Id).Returns(studentId);

            var enrollment = new Enrollment(Guid.NewGuid(), studentId, courseId, DateTime.UtcNow);
            var enrollments = new List<Enrollment> { enrollment };

            _mockEnrollmentRepository
                .Setup(r => r.GetStudentEnrollments(studentId))
                .ReturnsAsync(enrollments);

            _mockStorageService
                .Setup(s => s.SaveAsync(dummyStream, "jpg", It.IsAny<CancellationToken>()))
                .ReturnsAsync("evidence/failed_image.jpg");

            // Giả lập commit thất bại (quăng Exception)
            _mockUnitOfWork
                .Setup(u => u.CommitAsync(studentId.ToString()))
                .ThrowsAsync(new Exception("Database commit error"));

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("Database commit error");

            // Đảm bảo file đã lưu được xóa đi khi có lỗi xảy ra để tránh rác hệ thống
            _mockStorageService.Verify(s => s.DeleteAsync("evidence/failed_image.jpg"), Times.Once);
        }
    }
}
