using Application.Features.Statistics.Queries.GetSummaryStatistic;
using Application.Interface;
using Application.Results;
using Domain.AcademicManagement.Aggregate;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Enum;
using Domain.EnrollmentManagement.Aggregate;
using Domain.EnrollmentManagement.Enum;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Domain.OrderManagement.Aggregate;
using Domain.OrderManagement.Enum;
using Domain.OrderManagement.ValueObject;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Statistics.Queries.GetSummaryStatistic
{
    public class GetSummaryStatisticQueryHandlerTests
    {
        private readonly Mock<IApplicationDBContext> _mockContext;
        private readonly GetSummaryStatisticQueryHandler _handler;

        public GetSummaryStatisticQueryHandlerTests()
        {
            _mockContext = new Mock<IApplicationDBContext>();
            _handler = new GetSummaryStatisticQueryHandler(_mockContext.Object);
        }

        [Fact]
        public async Task Handle_NoDataInRange_ShouldReturnEmptySummariesAndZeroTotals()
        {
            // Arrange
            var query = new GetSummaryStatisticQuery
            {
                From = DateTime.UtcNow.AddDays(-5),
                To = DateTime.UtcNow
            };

            // Set up empty DbSets
            _mockContext.Setup(c => c.Users).Returns(DbSetMockHelper.CreateMockDbSet(new List<User>()).Object);
            _mockContext.Setup(c => c.Courses).Returns(DbSetMockHelper.CreateMockDbSet(new List<Course>()).Object);
            _mockContext.Setup(c => c.Enrollments).Returns(DbSetMockHelper.CreateMockDbSet(new List<Enrollment>()).Object);
            _mockContext.Setup(c => c.Orders).Returns(DbSetMockHelper.CreateMockDbSet(new List<Order>()).Object);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.User.Total.Should().Be(0);
            result.User.StudentCount.Should().Be(0);
            result.User.TeacherCount.Should().Be(0);

            result.Course.Total.Should().Be(0);
            result.Course.InReviewCount.Should().Be(0);
            result.Course.RejectedCount.Should().Be(0);
            result.Course.PublishedCount.Should().Be(0);

            result.Enrollment.Total.Should().Be(0);
            result.Enrollment.Completed.Should().Be(0);
            result.Enrollment.NotCompleted.Should().Be(0);

            result.Revenue.Total.Should().Be(0);
            result.Revenue.Commission.Should().Be(0);
            result.Revenue.TeacherFinance.Should().Be(0);

            result.CourseByGrade.Total.Should().Be(0);
            result.CourseByGrade.GradeCounts.Should().BeEmpty();

            result.CourseBySubject.Total.Should().Be(0);
            result.CourseBySubject.SubjectCounts.Should().BeEmpty();

            result.EnrollmentByGrade.Total.Should().Be(0);
            result.EnrollmentByGrade.GradeCounts.Should().BeEmpty();

            result.EnrollmentBySubject.Total.Should().Be(0);
            result.EnrollmentBySubject.SubjectCounts.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_WithDataInRange_ShouldReturnAccurateSummariesAndBreakdowns()
        {
            // Arrange
            var fromDate = new DateTime(2026, 6, 1);
            var toDate = new DateTime(2026, 6, 30);
            var targetDate = new DateTime(2026, 6, 15);

            var query = new GetSummaryStatisticQuery { From = fromDate, To = toDate };

            // 1. Setup Users
            var usersList = new List<User>
            {
                // Teacher in range
                CreateUserInstance(Guid.NewGuid(), "teacher@test.com", Role.Teacher, targetDate),
                // Students in range
                CreateUserInstance(Guid.NewGuid(), "student1@test.com", Role.Student, targetDate),
                CreateUserInstance(Guid.NewGuid(), "student2@test.com", Role.Student, targetDate),
                // Out of range user
                CreateUserInstance(Guid.NewGuid(), "out@test.com", Role.Student, new DateTime(2026, 5, 1))
            };

            // 2. Setup Courses, Grades, Subjects
            var grade10 = new Grade(Guid.NewGuid(), "Grade 10");
            var grade11 = new Grade(Guid.NewGuid(), "Grade 11");

            var mathSubject = new Subject(Guid.NewGuid(), "MATH", "Mathematics", grade10.GradeID);
            var physicsSubject = new Subject(Guid.NewGuid(), "PHYS", "Physics", grade10.GradeID);

            var course1 = CreateCourseInstance(Guid.NewGuid(), CourseStatus.Published, targetDate);
            SetPrivateProperty(course1, nameof(Course.Grade), grade10);
            SetPrivateProperty(course1, nameof(Course.Subject), mathSubject);

            var course2 = CreateCourseInstance(Guid.NewGuid(), CourseStatus.InReview, targetDate);
            SetPrivateProperty(course2, nameof(Course.Grade), grade10);
            SetPrivateProperty(course2, nameof(Course.Subject), physicsSubject);

            var course3 = CreateCourseInstance(Guid.NewGuid(), CourseStatus.Rejected, targetDate);
            SetPrivateProperty(course3, nameof(Course.Grade), grade11);
            SetPrivateProperty(course3, nameof(Course.Subject), mathSubject);

            var outOfRangeCourse = CreateCourseInstance(Guid.NewGuid(), CourseStatus.Published, new DateTime(2026, 5, 1));
            SetPrivateProperty(outOfRangeCourse, nameof(Course.Grade), grade10);
            SetPrivateProperty(outOfRangeCourse, nameof(Course.Subject), mathSubject);

            var coursesList = new List<Course> { course1, course2, course3, outOfRangeCourse };

            // 3. Setup Enrollments
            var enrollment1 = CreateEnrollmentInstance(Guid.NewGuid(), EnrollmentStatus.Completed, targetDate);
            SetPrivateProperty(enrollment1, nameof(Enrollment.Course), course1);

            var enrollment2 = CreateEnrollmentInstance(Guid.NewGuid(), EnrollmentStatus.Active, targetDate);
            SetPrivateProperty(enrollment2, nameof(Enrollment.Course), course2);

            var outOfRangeEnrollment = CreateEnrollmentInstance(Guid.NewGuid(), EnrollmentStatus.Completed, new DateTime(2026, 5, 1));
            SetPrivateProperty(outOfRangeEnrollment, nameof(Enrollment.Course), course1);

            var enrollmentsList = new List<Enrollment> { enrollment1, enrollment2, outOfRangeEnrollment };

            // 4. Setup Orders
            var commission1 = Commission.Create(0.15m, 100m); // Platform = 15, Teacher = 85
            var order1 = CreateOrderInstance(Guid.NewGuid(), commission1, targetDate, targetDate);

            var commission2 = Commission.Create(0.15m, 200m); // Platform = 30, Teacher = 170
            var order2 = CreateOrderInstance(Guid.NewGuid(), commission2, targetDate, targetDate);

            // Unpaid order - should not be included in revenue
            var orderUnpaid = CreateOrderInstance(Guid.NewGuid(), commission1, targetDate, null);

            // Out of range order
            var orderOutOfRange = CreateOrderInstance(Guid.NewGuid(), commission1, new DateTime(2026, 5, 1), new DateTime(2026, 5, 1));

            var ordersList = new List<Order> { order1, order2, orderUnpaid, orderOutOfRange };

            // Setup DB context mocks
            _mockContext.Setup(c => c.Users).Returns(DbSetMockHelper.CreateMockDbSet(usersList).Object);
            _mockContext.Setup(c => c.Courses).Returns(DbSetMockHelper.CreateMockDbSet(coursesList).Object);
            _mockContext.Setup(c => c.Enrollments).Returns(DbSetMockHelper.CreateMockDbSet(enrollmentsList).Object);
            _mockContext.Setup(c => c.Orders).Returns(DbSetMockHelper.CreateMockDbSet(ordersList).Object);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();

            // Users: Total 4 (1 teacher, 3 students)
            result.User.Total.Should().Be(4);
            result.User.TeacherCount.Should().Be(1);
            result.User.StudentCount.Should().Be(3);

            // Courses: Total 4 (2 Published, 1 InReview, 1 Rejected)
            result.Course.Total.Should().Be(4);
            result.Course.PublishedCount.Should().Be(2);
            result.Course.InReviewCount.Should().Be(1);
            result.Course.RejectedCount.Should().Be(1);

            // Enrollments: Total 3 (2 Completed, 1 Active)
            result.Enrollment.Total.Should().Be(3);
            result.Enrollment.Completed.Should().Be(2);
            result.Enrollment.NotCompleted.Should().Be(1);

            // Revenue: Total = 100+200 = 300, Commission = 15+30 = 45, TeacherFinance = 85+170 = 255
            result.Revenue.Total.Should().Be(300);
            result.Revenue.Commission.Should().Be(45);
            result.Revenue.TeacherFinance.Should().Be(255);

            // Breakdowns:
            // Course by Grade: Grade 10 (course1, course2, outOfRangeCourse) = 3, Grade 11 (course3) = 1. Total = 4
            result.CourseByGrade.Total.Should().Be(4);
            result.CourseByGrade.GradeCounts.Should().HaveCount(2);
            result.CourseByGrade.GradeCounts["Grade 10"].Should().Be(3);
            result.CourseByGrade.GradeCounts["Grade 11"].Should().Be(1);

            // Course by Subject: Mathematics (course1, course3, outOfRangeCourse) = 3, Physics (course2) = 1. Total = 4
            result.CourseBySubject.Total.Should().Be(4);
            result.CourseBySubject.SubjectCounts.Should().HaveCount(2);
            result.CourseBySubject.SubjectCounts["Mathematics"].Should().Be(3);
            result.CourseBySubject.SubjectCounts["Physics"].Should().Be(1);

            // Enrollment by Grade: Grade 10 (enrollment1, enrollment2, outOfRangeEnrollment) = 3. Total = 3
            result.EnrollmentByGrade.Total.Should().Be(3);
            result.EnrollmentByGrade.GradeCounts.Should().HaveCount(1);
            result.EnrollmentByGrade.GradeCounts["Grade 10"].Should().Be(3);

            // Enrollment by Subject: Mathematics (enrollment1, outOfRangeEnrollment) = 2, Physics (enrollment2) = 1. Total = 3
            result.EnrollmentBySubject.Total.Should().Be(3);
            result.EnrollmentBySubject.SubjectCounts.Should().HaveCount(2);
            result.EnrollmentBySubject.SubjectCounts["Mathematics"].Should().Be(2);
            result.EnrollmentBySubject.SubjectCounts["Physics"].Should().Be(1);
        }

        private User CreateUserInstance(Guid userId, string email, Role role, DateTime createdAt)
        {
            var user = new User(
                userId,
                email,
                "password123",
                "0123456789",
                "Test User",
                null,
                role,
                createdAt,
                isVerified: true
            );
            return user;
        }

        private Course CreateCourseInstance(Guid courseId, CourseStatus status, DateTime createdAt)
        {
            var course = new Course(
                courseId,
                "Course Name",
                "Description",
                100m,
                "thumbnail.png",
                "slug",
                "Prerequisites",
                "Outcomes",
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                createdAt
            );
            SetPrivateProperty(course, nameof(Course.Status), status);
            return course;
        }

        private Enrollment CreateEnrollmentInstance(Guid enrollmentId, EnrollmentStatus status, DateTime enrolledAt)
        {
            var enrollment = new Enrollment(
                enrollmentId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                enrolledAt
            );
            SetPrivateProperty(enrollment, nameof(Enrollment.Status), status);
            return enrollment;
        }

        private Order CreateOrderInstance(Guid orderId, Commission commission, DateTime createdAt, DateTime? paidAt)
        {
            var order = new Order(
                orderId,
                commission,
                Guid.NewGuid(),
                Guid.NewGuid(),
                createdAt
            );

            // Set private fields/properties
            SetPrivateProperty(order, nameof(Order.PaidAt), paidAt);
            if (paidAt.HasValue)
            {
                SetPrivateProperty(order, nameof(Order.Status), OrderStatus.Pending);
            }
            return order;
        }

        private void SetPrivateProperty(object target, string propertyName, object? value)
        {
            var prop = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            prop?.SetValue(target, value);
        }
    }
}
