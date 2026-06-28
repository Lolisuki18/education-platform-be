using Application.BusinessException;
using Application.Features.Complaints.Queries.GetComplaintDetail;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using FluentAssertions;
using Moq;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Complaints.Queries.GetComplaintDetail
{
    public class GetComplaintDetailQueryHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICourseRepository> _mockCourseRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly GetComplaintDetailQueryHandler _handler;

        public GetComplaintDetailQueryHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCourseRepository = new Mock<ICourseRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockCurrentUser = new Mock<ICurrentUser>();

            // Mock UnitOfWork để trả về Mock CourseRepository khi GetRepository<ICourseRepository>() được gọi
            _mockUnitOfWork
                .Setup(u => u.GetRepository<ICourseRepository>())
                .Returns(_mockCourseRepository.Object);

            _handler = new GetComplaintDetailQueryHandler(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockCurrentUser.Object);
        }

        [Fact]
        public async Task Handle_ComplaintDoesNotExist_ShouldThrowNotFoundException()
        {
            // Arrange
            var query = new GetComplaintDetailQuery { ComplaintID = Guid.NewGuid() };

            // Mock repository trả về null để giả lập không tìm thấy Complaint
            _mockCourseRepository
                .Setup(r => r.GetComplaintDetailByID(query.ComplaintID))
                .ReturnsAsync((Complaint?)null);

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFound>()
                .WithMessage($"Complaint with ID: {query.ComplaintID} is not found");
        }

        [Fact]
        public async Task Handle_UserIsTeacherAndNotCourseOwner_ShouldThrowForbiddenException()
        {
            // Arrange
            var complaintId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            var studentId = Guid.NewGuid();
            var actualTeacherId = Guid.NewGuid();
            var accessingTeacherId = Guid.NewGuid(); // Giáo viên truy cập khác với giáo viên sở hữu khóa học

            var query = new GetComplaintDetailQuery { ComplaintID = complaintId };

            // Khởi tạo Course và Complaint
            var course = CreateCourseInstance(courseId, actualTeacherId);
            var complaint = new Complaint(complaintId, courseId, studentId, "Reason for complaint", null);
            SetPrivateProperty(complaint, nameof(Complaint.Course), course);

            // Mock repository trả về Complaint đã chuẩn bị
            _mockCourseRepository
                .Setup(r => r.GetComplaintDetailByID(complaintId))
                .ReturnsAsync(complaint);

            // Giả lập người đăng nhập là Giáo viên có ID khác
            _mockCurrentUser.Setup(u => u.Role).Returns("Teacher");
            _mockCurrentUser.Setup(u => u.Id).Returns(accessingTeacherId);

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ForbiddenException>()
                .WithMessage("You do not have permission to view this complaint.");
        }

        [Fact]
        public async Task Handle_ValidRequest_ShouldReturnMappedComplaintDetailDTO()
        {
            // Arrange
            var complaintId = Guid.NewGuid();
            var courseId = Guid.NewGuid();
            var studentId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();

            var query = new GetComplaintDetailQuery { ComplaintID = complaintId };

            var course = CreateCourseInstance(courseId, teacherId);
            var complaint = new Complaint(complaintId, courseId, studentId, "Reason for complaint", null);
            SetPrivateProperty(complaint, nameof(Complaint.Course), course);

            var expectedDto = new ComplaintDetailDTO { ComplaintID = complaintId };

            // Mock repository trả về Complaint hợp lệ
            _mockCourseRepository
                .Setup(r => r.GetComplaintDetailByID(complaintId))
                .ReturnsAsync(complaint);

            // Giả lập user đăng nhập là chính giáo viên của khóa học
            _mockCurrentUser.Setup(u => u.Role).Returns("Teacher");
            _mockCurrentUser.Setup(u => u.Id).Returns(teacherId);

            // Giả lập mapper hoạt động
            _mockMapper
                .Setup(m => m.Map<ComplaintDetailDTO>(complaint))
                .Returns(expectedDto);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.ComplaintID.Should().Be(complaintId);
        }

        // Helper method sử dụng Reflection để gán dữ liệu cho các Navigation Properties có private setter
        private void SetPrivateProperty(object target, string propertyName, object value)
        {
            var prop = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            prop?.SetValue(target, value);
        }

        // Helper để khởi tạo thực thể Course
        private Course CreateCourseInstance(Guid courseId, Guid teacherId)
        {
            return new Course(
                courseId,
                "Test Course",
                "Description",
                100,
                "thumbnail.png",
                "test-course",
                "Prerequisites",
                "Outcomes",
                teacherId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                DateTime.UtcNow
            );
        }
    }
}
