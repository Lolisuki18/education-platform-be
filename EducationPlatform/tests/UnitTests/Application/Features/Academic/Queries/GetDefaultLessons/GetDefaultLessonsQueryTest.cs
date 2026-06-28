using Application.BusinessException;
using Application.Features.Academic.Queries.GetDefaultLessons;
using Application.Features.Complaints.Queries.GetComplaintDetail;
using Application.Results;
using AutoMapper;
using Domain.AcademicManagement.Aggregate;
using Domain.AcademicManagement.Entity;
using Domain.Common.Interfaces;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;

namespace UnitTests.Application.Features.Academic.Queries.GetDefaultLessons
{
    public class GetDefaultLessonsQueryTest
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IMapper> _mockMapper;
        private readonly GetDefaultLessonsQueryHandler _mockHandler;
        private readonly Mock<ISubjectRepository> _mockISubjectRepository;

        public GetDefaultLessonsQueryTest()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockMapper = new Mock<IMapper>();
            _mockISubjectRepository = new Mock<ISubjectRepository>();

            // Mock UnitOfWork trả về SubjectRepository
            _mockUnitOfWork.Setup(u => u.GetRepository<ISubjectRepository>())
                        .Returns(_mockISubjectRepository.Object); 
            //Khởi tạo handler
            _mockHandler = new GetDefaultLessonsQueryHandler(_mockUnitOfWork.Object,_mockMapper.Object);
        }

        [Fact]
        public async Task Handle_DefaultLessonsListIsEmptyOrNull_ShouldThrowNotFoundException()
        {
            //1.Arrange : prepare for query
            // initialize subjectId and gradeId for request query
            var subjectId = Guid.NewGuid();
            var gradeId = Guid.NewGuid();

            //create query object with subjectId and gradeId
            var query = new GetDefaultLessonsQuery { SubjectId = subjectId , GradeId = gradeId};

            //simulate the repository to return an empty list for default lessons
            _mockISubjectRepository.Setup(r => r.GetDefaultLessons(query.SubjectId, query.GradeId))
                                    .ReturnsAsync(new List<DefaultLesson>());

            //2.Act: call the handler to handle the query
            Func<Task> act = async () => await _mockHandler.Handle(query, CancellationToken.None);

            // 3. Assert: verify that the handler throws a NotFoundException when the list of default lessons is empty
            await act.Should().ThrowAsync<NotFound>().WithMessage("No default lessons found for the specified subject and grade.");
        }

        [Fact]
        public async Task Handle_ValidRequest_ShouldReturnMappedDefaultLessonsList()
        {
            // 1. Arrange: Chuẩn bị dữ liệu
            var subjectId = Guid.NewGuid();
            var gradeId = Guid.NewGuid();
            var query = new GetDefaultLessonsQuery { SubjectId = subjectId, GradeId = gradeId };
            // Khởi tạo đối tượng DefaultLesson thật để đưa vào danh sách trả về
            var lesson = new DefaultLesson(
                defaultLessonId: Guid.NewGuid(),
                objectives: "Test objectives",
                description: "Test description",
                name: "Lesson 1",
                gradeId: gradeId,
                subjectId: subjectId
            );
            var lessonList = new List<DefaultLesson> { lesson };
            // Cài đặt cho Mock Repository trả về danh sách vừa tạo
            _mockISubjectRepository
                .Setup(r => r.GetDefaultLessons(subjectId, gradeId))
                .ReturnsAsync(lessonList);
            // Giả lập DTO đầu ra sau khi Mapper thực hiện map dữ liệu
            var expectedDtos = new List<DefaultLessonDTO>
            {
                new DefaultLessonDTO { DefaultLessonID = lesson.DefaultLessonID, Name = "Lesson 1" }
            };
            _mockMapper
                .Setup(m => m.Map<IEnumerable<DefaultLessonDTO>>(lessonList))
                .Returns(expectedDtos);
            // 2. Act: Thực thi
            var result = await _mockHandler.Handle(query, CancellationToken.None);
            // 3. Assert: Kiểm chứng kết quả trả về
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result.First().DefaultLessonID.Should().Be(lesson.DefaultLessonID);
        }
    }
}
