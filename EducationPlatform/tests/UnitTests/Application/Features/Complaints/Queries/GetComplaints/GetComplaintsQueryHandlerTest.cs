using Application.Features.Complaints.Queries.GetComplaints;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Enum;
using Domain.IdentityManagement.Enum;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Complaints.Queries.GetComplaints
{
    public class GetComplaintsQueryHandlerTest
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICourseRepository> _mockCourseRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly GetComplaintsQueryHandler _handler;

        public GetComplaintsQueryHandlerTest()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCourseRepository = new Mock<ICourseRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockCurrentUser = new Mock<ICurrentUser>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<ICourseRepository>())
                .Returns(_mockCourseRepository.Object);

            _handler = new GetComplaintsQueryHandler(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockCurrentUser.Object);
        }

        [Fact]
        public async Task Handle_CurrentUserIsTeacher_ShouldPassTeacherIdToRepositoryAndReturnMappedComplaints()
        {
            // Arrange
            var teacherId = Guid.NewGuid();
            var query = new GetComplaintsQuery { Status = ComplaintStatus.Pending };

            var complaints = new List<Complaint>
            {
                new Complaint(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Reason 1", null)
            };

            var expectedDtos = new List<ComplaintDTO>
            {
                new ComplaintDTO { ComplaintID = Guid.NewGuid(), Reason = "Reason 1" }
            };

            _mockCurrentUser.Setup(u => u.Role).Returns(Role.Teacher.ToString());
            _mockCurrentUser.Setup(u => u.Id).Returns(teacherId);

            _mockCourseRepository
                .Setup(r => r.GetComplaintsAsync(query.Status, teacherId))
                .ReturnsAsync(complaints);

            _mockMapper
                .Setup(m => m.Map<IEnumerable<ComplaintDTO>>(complaints))
                .Returns(expectedDtos);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedDtos);

            _mockCourseRepository.Verify(r => r.GetComplaintsAsync(query.Status, teacherId), Times.Once);
        }

        [Fact]
        public async Task Handle_CurrentUserIsNotTeacher_ShouldPassNullTeacherIdToRepositoryAndReturnMappedComplaints()
        {
            // Arrange
            var query = new GetComplaintsQuery { Status = ComplaintStatus.Approved };

            var complaints = new List<Complaint>
            {
                new Complaint(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Reason 2", null)
            };

            var expectedDtos = new List<ComplaintDTO>
            {
                new ComplaintDTO { ComplaintID = Guid.NewGuid(), Reason = "Reason 2" }
            };

            // Giả lập user hiện tại có Role là Admin (không phải Teacher)
            _mockCurrentUser.Setup(u => u.Role).Returns(Role.Admin.ToString());

            _mockCourseRepository
                .Setup(r => r.GetComplaintsAsync(query.Status, null))
                .ReturnsAsync(complaints);

            _mockMapper
                .Setup(m => m.Map<IEnumerable<ComplaintDTO>>(complaints))
                .Returns(expectedDtos);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedDtos);

            _mockCourseRepository.Verify(r => r.GetComplaintsAsync(query.Status, null), Times.Once);
        }
    }
}
