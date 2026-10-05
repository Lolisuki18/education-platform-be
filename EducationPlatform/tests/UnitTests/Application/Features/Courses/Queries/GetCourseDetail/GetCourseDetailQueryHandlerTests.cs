using Application.Exceptions;
using Application.Features.Courses.Queries.GetCourseDetail;
using Application.Interface;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Enum;
using Domain.IdentityManagement.Enum;
using Domain.IdentityManagement.ValueObject;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Courses.Queries.GetCourseDetail
{
    public class GetCourseDetailQueryHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICourseRepository> _mockCourseRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICurrentUser> _mockCurrentUser;
        private readonly Mock<IMediaUrlSigner> _mockSigner;
        private readonly GetCourseDetailQueryHandler _handler;

        public GetCourseDetailQueryHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCourseRepository = new Mock<ICourseRepository>();
            _mockMapper = new Mock<IMapper>();
            _mockCurrentUser = new Mock<ICurrentUser>();
            _mockSigner = new Mock<IMediaUrlSigner>();
            _mockSigner.Setup(x => x.Protect(It.IsAny<string>())).Returns((string url) => "signed:" + url);

            _mockUnitOfWork
                .Setup(u => u.GetRepository<ICourseRepository>())
                .Returns(_mockCourseRepository.Object);

            _handler = new GetCourseDetailQueryHandler(
                _mockUnitOfWork.Object,
                _mockMapper.Object,
                _mockCurrentUser.Object,
                _mockSigner.Object);
        }

        [Fact]
        public async Task Handle_InvalidRole_ShouldThrowAuthenticateException()
        {
            // Arrange
            var query = new GetCourseDetailQuery { CourseID = Guid.NewGuid() };
            _mockCurrentUser.Setup(u => u.IsAuthenticated).Returns(true);
            _mockCurrentUser.Setup(u => u.Role).Returns("InvalidRoleName");

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<AuthenticateException>().WithMessage("Invalid role");
        }

        [Fact]
        public async Task Handle_CourseDoesNotExist_ShouldThrowNotFoundException()
        {
            // Arrange
            var courseId = Guid.NewGuid();
            var query = new GetCourseDetailQuery { CourseID = courseId };
            _mockCurrentUser.Setup(u => u.IsAuthenticated).Returns(false);

            _mockCourseRepository
                .Setup(r => r.GetCourseMetadataByID(courseId))
                .ReturnsAsync((Course?)null);

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Course with ID: {courseId} is not found");
        }

        [Fact]
        public async Task Handle_AnonymousOrStudentOnDraftCourse_ShouldThrowNotFoundException()
        {
            // Arrange
            var courseId = Guid.NewGuid();
            var query = new GetCourseDetailQuery { CourseID = courseId };
            _mockCurrentUser.Setup(u => u.IsAuthenticated).Returns(true);
            _mockCurrentUser.Setup(u => u.Role).Returns(Role.Student.ToString());

            // Course is in review (not published)
            var course = CreateCourseInstance(courseId, Guid.NewGuid(), CourseStatus.InReview);
            _mockCourseRepository
                .Setup(r => r.GetCourseMetadataByID(courseId))
                .ReturnsAsync(course);

            // Act
            Func<Task> act = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Course with ID: {courseId} is not found");
        }

        [Fact]
        public async Task Handle_AnonymousUserOnPublishedCourse_ShouldReturnMetadataOnly()
        {
            // Arrange
            var courseId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var query = new GetCourseDetailQuery { CourseID = courseId };
            _mockCurrentUser.Setup(u => u.IsAuthenticated).Returns(false);

            // Course is published
            var course = CreateCourseInstance(courseId, teacherId, CourseStatus.Published);
            _mockCourseRepository
                .Setup(r => r.GetCourseMetadataByID(courseId))
                .ReturnsAsync(course);

            var expectedDto = new CourseDetailDTO
            {
                CourseID = courseId,
                Title = "Published Course",
                Chapters = new List<ChapterDTO> { new ChapterDTO { ChapterID = Guid.NewGuid(), Title = "Chapter 1" } }
            };

            _mockMapper.Setup(m => m.Map<CourseDetailDTO>(course)).Returns(expectedDto);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Title.Should().Be("Published Course");
            // Chapters list should be cleared out for anonymous users
            result.Chapters.Should().BeEmpty();

            _mockCourseRepository.Verify(r => r.GetCourseDetailByID(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task Handle_TeacherOwner_ShouldReturnFullCourseDetailsWithChapters()
        {
            // Arrange
            var courseId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var query = new GetCourseDetailQuery { CourseID = courseId };

            _mockCurrentUser.Setup(u => u.IsAuthenticated).Returns(true);
            _mockCurrentUser.Setup(u => u.Role).Returns(Role.Teacher.ToString());
            _mockCurrentUser.Setup(u => u.Id).Returns(teacherId);

            var metadataCourse = CreateCourseInstance(courseId, teacherId, CourseStatus.InReview);
            _mockCourseRepository
                .Setup(r => r.GetCourseMetadataByID(courseId))
                .ReturnsAsync(metadataCourse);

            var detailedCourse = CreateCourseInstance(courseId, teacherId, CourseStatus.InReview);
            _mockCourseRepository
                .Setup(r => r.GetCourseDetailByID(courseId))
                .ReturnsAsync(detailedCourse);

            var expectedDto = new CourseDetailDTO
            {
                CourseID = courseId,
                Title = "Teacher Course",
                Chapters = new List<ChapterDTO> { new ChapterDTO { ChapterID = Guid.NewGuid(), Title = "Chapter 1" } }
            };

            _mockMapper.Setup(m => m.Map<CourseDetailDTO>(detailedCourse)).Returns(expectedDto);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Chapters.Should().NotBeEmpty();
            result.Chapters.First().Title.Should().Be("Chapter 1");

            _mockCourseRepository.Verify(r => r.GetCourseDetailByID(courseId), Times.Once);
        }

        [Fact]
        public async Task Handle_TeacherNonOwner_ShouldReturnMetadataOnly()
        {
            // Arrange
            var courseId = Guid.NewGuid();
            var ownerTeacherId = Guid.NewGuid();
            var accessingTeacherId = Guid.NewGuid();
            var query = new GetCourseDetailQuery { CourseID = courseId };

            _mockCurrentUser.Setup(u => u.IsAuthenticated).Returns(true);
            _mockCurrentUser.Setup(u => u.Role).Returns(Role.Teacher.ToString());
            _mockCurrentUser.Setup(u => u.Id).Returns(accessingTeacherId);

            var course = CreateCourseInstance(courseId, ownerTeacherId, CourseStatus.Published);
            _mockCourseRepository
                .Setup(r => r.GetCourseMetadataByID(courseId))
                .ReturnsAsync(course);

            var expectedDto = new CourseDetailDTO
            {
                CourseID = courseId,
                Title = "Published Course",
                Chapters = new List<ChapterDTO> { new ChapterDTO { ChapterID = Guid.NewGuid(), Title = "Chapter 1" } }
            };

            _mockMapper.Setup(m => m.Map<CourseDetailDTO>(course)).Returns(expectedDto);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Chapters.Should().BeEmpty();

            _mockCourseRepository.Verify(r => r.GetCourseDetailByID(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task Handle_AdminUser_ShouldReturnFullCourseDetailsEvenIfDraft()
        {
            // Arrange
            var courseId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            var query = new GetCourseDetailQuery { CourseID = courseId };

            _mockCurrentUser.Setup(u => u.IsAuthenticated).Returns(true);
            _mockCurrentUser.Setup(u => u.Role).Returns(Role.Admin.ToString());

            var metadataCourse = CreateCourseInstance(courseId, teacherId, CourseStatus.InReview);
            _mockCourseRepository
                .Setup(r => r.GetCourseMetadataByID(courseId))
                .ReturnsAsync(metadataCourse);

            var detailedCourse = CreateCourseInstance(courseId, teacherId, CourseStatus.InReview);
            _mockCourseRepository
                .Setup(r => r.GetCourseDetailByID(courseId))
                .ReturnsAsync(detailedCourse);

            var expectedDto = new CourseDetailDTO
            {
                CourseID = courseId,
                Title = "Draft Course for Admin",
                Chapters = new List<ChapterDTO> { new ChapterDTO { ChapterID = Guid.NewGuid(), Title = "Chapter 1" } }
            };

            _mockMapper.Setup(m => m.Map<CourseDetailDTO>(detailedCourse)).Returns(expectedDto);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Chapters.Should().NotBeEmpty();

            _mockCourseRepository.Verify(r => r.GetCourseDetailByID(courseId), Times.Once);
        }

        [Fact]
        public async Task Handle_TeacherOwner_ShouldReceiveSignedVideoLinks()
        {
            var courseId = Guid.NewGuid();
            var teacherId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.IsAuthenticated).Returns(true);
            _mockCurrentUser.Setup(u => u.Role).Returns(Role.Teacher.ToString());
            _mockCurrentUser.Setup(u => u.Id).Returns(teacherId);

            var course = CreateCourseInstance(courseId, teacherId, CourseStatus.InReview);
            _mockCourseRepository.Setup(r => r.GetCourseMetadataByID(courseId)).ReturnsAsync(course);
            _mockCourseRepository.Setup(r => r.GetCourseDetailByID(courseId)).ReturnsAsync(course);
            _mockMapper.Setup(m => m.Map<CourseDetailDTO>(course)).Returns(new CourseDetailDTO
            {
                CourseID = courseId,
                Chapters = new List<ChapterDTO>
                {
                    new() { Lessons = new List<LessonDTO> { new() { VideoUrl = "videos/a.mp4" } } }
                }
            });

            var result = await _handler.Handle(new GetCourseDetailQuery { CourseID = courseId }, CancellationToken.None);

            result.Chapters[0].Lessons[0].VideoUrl.Should().Be("signed:videos/a.mp4");
        }

        [Fact]
        public async Task Handle_Student_ShouldNeverGetVideoLinksOfACourseHeHasNotEntered()
        {
            var courseId = Guid.NewGuid();
            _mockCurrentUser.Setup(u => u.IsAuthenticated).Returns(true);
            _mockCurrentUser.Setup(u => u.Role).Returns(Role.Student.ToString());
            _mockCurrentUser.Setup(u => u.Id).Returns(Guid.NewGuid());

            var course = CreateCourseInstance(courseId, Guid.NewGuid(), CourseStatus.Published);
            _mockCourseRepository.Setup(r => r.GetCourseMetadataByID(courseId)).ReturnsAsync(course);
            _mockMapper.Setup(m => m.Map<CourseDetailDTO>(course)).Returns(new CourseDetailDTO
            {
                CourseID = courseId,
                Chapters = new List<ChapterDTO> { new() { Lessons = new List<LessonDTO> { new() { VideoUrl = "videos/a.mp4" } } } }
            });

            var result = await _handler.Handle(new GetCourseDetailQuery { CourseID = courseId }, CancellationToken.None);

            result.Chapters.Should().BeEmpty();
            _mockSigner.Verify(x => x.Protect(It.IsAny<string>()), Times.Never);
        }

        private Course CreateCourseInstance(Guid courseId, Guid teacherId, CourseStatus status)
        {
            var course = new Course(
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

            // Set the Status property using reflection since it has a private setter
            var statusProp = typeof(Course).GetProperty(nameof(Course.Status));
            statusProp?.SetValue(course, status);

            return course;
        }
    }
}
