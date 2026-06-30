using Application.BusinessException;
using Application.Features.Complaints.Commands.ReviewComplaint;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Enum;
using FluentAssertions;
using MediatR;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Complaints.Commands.ReviewComplaint
{
    public class ReviewComplaintCommandHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IComplaintRepository> _mockComplaintRepository;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly ReviewComplaintCommandHandler _handler;

        public ReviewComplaintCommandHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockComplaintRepository = new Mock<IComplaintRepository>();
            _mockCurrentUser = new Mock<ICurrentUser>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IComplaintRepository>())
                .Returns(_mockComplaintRepository.Object);

            _handler = new ReviewComplaintCommandHandler(
                _mockUnitOfWork.Object,
                _mockCurrentUser.Object);
        }

        [Fact]
        public async Task Handle_UserNotAuthenticated_ShouldThrowAuthenticateException()
        {
            // Arrange
            _mockCurrentUser.Setup(u => u.Id).Returns((Guid?)null);
            var command = new ReviewComplaintCommand
            {
                ComplaintID = Guid.NewGuid(),
                IsApproved = true,
                AdminNote = "Looks good"
            };

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>()
                .WithMessage("User must be authenticated.");
        }

        [Fact]
        public async Task Handle_ComplaintDoesNotExist_ShouldThrowNotFoundException()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var complaintId = Guid.NewGuid();
            var command = new ReviewComplaintCommand
            {
                ComplaintID = complaintId,
                IsApproved = true,
                AdminNote = "Approved"
            };

            _mockCurrentUser.Setup(u => u.Id).Returns(adminId);

            _mockComplaintRepository
                .Setup(r => r.GetComplaintDetailByID(complaintId))
                .ReturnsAsync((Complaint?)null);

            // Act
            Func<Task> act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFound>()
                .WithMessage($"Complaint with ID: {complaintId} is not found");
        }

        [Fact]
        public async Task Handle_ValidApprovedRequest_ShouldApproveComplaintAndUpdateSuccessfully()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var complaintId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            var studentId = Guid.NewGuid();
            var command = new ReviewComplaintCommand
            {
                ComplaintID = complaintId,
                IsApproved = true,
                AdminNote = "Violated policies found, approving complaint."
            };

            _mockCurrentUser.Setup(u => u.Id).Returns(adminId);

            var complaint = new Complaint(complaintId, courseId, studentId, "reason", null);

            _mockComplaintRepository
                .Setup(r => r.GetComplaintDetailByID(complaintId))
                .ReturnsAsync(complaint);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().Be(Unit.Value);

            complaint.Status.Should().Be(ComplaintStatus.Approved);
            complaint.AdminNote.Should().Be("Violated policies found, approving complaint.");

            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _mockComplaintRepository.Verify(r => r.UpdateComplaint(complaint), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(adminId.ToString()), Times.Once);
        }

        [Fact]
        public async Task Handle_ValidRejectedRequest_ShouldRejectComplaintAndUpdateSuccessfully()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var complaintId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            var studentId = Guid.NewGuid();
            var command = new ReviewComplaintCommand
            {
                ComplaintID = complaintId,
                IsApproved = false,
                AdminNote = "No issues found, rejecting complaint."
            };

            _mockCurrentUser.Setup(u => u.Id).Returns(adminId);

            var complaint = new Complaint(complaintId, courseId, studentId, "reason", null);

            _mockComplaintRepository
                .Setup(r => r.GetComplaintDetailByID(complaintId))
                .ReturnsAsync(complaint);

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            result.Should().Be(Unit.Value);

            complaint.Status.Should().Be(ComplaintStatus.Rejected);
            complaint.AdminNote.Should().Be("No issues found, rejecting complaint.");

            _mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
            _mockComplaintRepository.Verify(r => r.UpdateComplaint(complaint), Times.Once);
            _mockUnitOfWork.Verify(u => u.CommitAsync(adminId.ToString()), Times.Once);
        }
    }
}
