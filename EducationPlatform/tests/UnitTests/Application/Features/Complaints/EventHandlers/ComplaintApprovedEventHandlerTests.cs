using Application.Features.Complaints.EventHandlers;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Enum;
using Domain.CourseManagement.Events;
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

namespace UnitTests.Application.Features.Complaints.EventHandlers
{
    public class ComplaintApprovedEventHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICourseRepository> _mockCourseRepository;
        private readonly Mock<IComplaintRepository> _mockComplaintRepository;
        private readonly Mock<IEnrollmentRepository> _mockEnrollmentRepository;
        private readonly Mock<IOrderRepository> _mockOrderRepository;
        private readonly ComplaintApprovedEventHandler _handler;

        public ComplaintApprovedEventHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCourseRepository = new Mock<ICourseRepository>();
            _mockComplaintRepository = new Mock<IComplaintRepository>();
            _mockEnrollmentRepository = new Mock<IEnrollmentRepository>();
            _mockOrderRepository = new Mock<IOrderRepository>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<ICourseRepository>())
                .Returns(_mockCourseRepository.Object);

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IComplaintRepository>())
                .Returns(_mockComplaintRepository.Object);

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IEnrollmentRepository>())
                .Returns(_mockEnrollmentRepository.Object);

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IOrderRepository>())
                .Returns(_mockOrderRepository.Object);

            _handler = new ComplaintApprovedEventHandler(_mockUnitOfWork.Object);
        }

        [Fact]
        public async Task Handle_FirstApprovedComplaint_ShouldMarkCourseAsRejectedAndNotCreateCouponsOrPenalties()
        {
            // Arrange
            var courseId = Guid.NewGuid();
            var complaintId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var notification = new ComplaintApprovedEvent(complaintId, courseId);

            // Giả lập chưa có khiếu nại được duyệt trước đó (tổng số khiếu nại = 0 + 1 = 1 < 2)
            _mockComplaintRepository
                .Setup(r => r.GetApprovedByCoursesAsync(courseId))
                .ReturnsAsync(new List<Complaint>());

            var course = CreateCourseInstance(courseId, teacherId, 100);

            _mockCourseRepository
                .Setup(r => r.GetByIdAsync(courseId))
                .ReturnsAsync(course);

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            course.Status.Should().Be(CourseStatus.Rejected);
            course.AdminNote.Should().Be("Rejected due to approved complaint.");

            _mockCourseRepository.Verify(r => r.Update(courseId, course), Times.Once);
            _mockOrderRepository.Verify(r => r.CreateCoupons(It.IsAny<IEnumerable<Coupon>>()), Times.Never);
            _mockOrderRepository.Verify(r => r.CreatePenalty(It.IsAny<Penalty>()), Times.Never);
            _mockComplaintRepository.Verify(r => r.RemoveComplaints(It.IsAny<IEnumerable<Complaint>>()), Times.Never);
        }

        [Fact]
        public async Task Handle_SecondApprovedComplaint_ShouldRejectByComplaintCreateCouponsAndPenaltiesAndCleanUpComplaints()
        {
            // Arrange
            var courseId = Guid.NewGuid();
            var complaintId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var notification = new ComplaintApprovedEvent(complaintId, courseId);

            // Giả lập đã có 1 khiếu nại được duyệt trước đó (tổng số khiếu nại = 1 + 1 = 2 >= 2)
            var studentId1 = Guid.NewGuid();
            var previousComplaint = new Complaint(Guid.NewGuid(), courseId, studentId1, "Previous reason", null);
            var approvedComplaints = new List<Complaint> { previousComplaint };

            _mockComplaintRepository
                .Setup(r => r.GetApprovedByCoursesAsync(courseId))
                .ReturnsAsync(approvedComplaints);

            var course = CreateCourseInstance(courseId, teacherId, 100); // Giá khóa học là 100

            _mockCourseRepository
                .Setup(r => r.GetByIdAsync(courseId))
                .ReturnsAsync(course);

            // Giả lập khóa học có 2 học sinh đang học
            var studentId2 = Guid.NewGuid();
            var studentIds = new List<Guid> { studentId1, studentId2 };
            _mockEnrollmentRepository
                .Setup(r => r.GetEnrolledStudentIdsByCourseId(courseId))
                .ReturnsAsync(studentIds);

            // Giả lập khiếu nại hiện tại
            var currentComplaint = new Complaint(complaintId, courseId, studentId2, "Current reason", null);
            _mockComplaintRepository
                .Setup(r => r.GetComplaintDetailByID(complaintId))
                .ReturnsAsync(currentComplaint);

            // Act
            await _handler.Handle(notification, CancellationToken.None);

            // Assert
            course.Status.Should().Be(CourseStatus.Rejected);
            course.AdminNote.Should().Be("Course removed due to multiple approved complaints.");

            _mockCourseRepository.Verify(r => r.Update(courseId, course), Times.Once);

            // Kiểm tra bồi thường học sinh: 100 * 7.5% = 7.5 cho mỗi học sinh
            _mockOrderRepository.Verify(r => r.CreateCoupons(It.Is<IEnumerable<Coupon>>(coupons =>
                coupons.Count() == 2 &&
                coupons.All(c => c.DiscountAmount == 7.5m) &&
                coupons.Any(c => c.StudentID == studentId1) &&
                coupons.Any(c => c.StudentID == studentId2)
            )), Times.Once);

            // Kiểm tra phạt giáo viên: tổng bồi thường = 7.5 * 2 = 15
            _mockOrderRepository.Verify(r => r.CreatePenalty(It.Is<Penalty>(p =>
                p.CourseID == courseId &&
                p.TeacherID == teacherId &&
                p.PenaltyAmount == 15m
            )), Times.Once);

            // Kiểm tra xóa lịch sử khiếu nại để giải quyết
            _mockComplaintRepository.Verify(r => r.RemoveComplaints(It.Is<IEnumerable<Complaint>>(list =>
                list.Count() == 2 &&
                list.Contains(previousComplaint) &&
                list.Contains(currentComplaint)
            )), Times.Once);
        }

        private Course CreateCourseInstance(Guid courseId, Guid teacherId, decimal priceAmount)
        {
            return new Course(
                courseId,
                "Test Course",
                "Description",
                priceAmount,
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
