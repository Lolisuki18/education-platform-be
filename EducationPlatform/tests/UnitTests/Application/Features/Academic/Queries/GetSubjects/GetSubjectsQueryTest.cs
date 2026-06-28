using Application.BusinessException;
using Application.Features.Academic.Queries.GetSubjects;
using Application.Results;
using AutoMapper;
using Domain.AcademicManagement.Aggregate;
using Domain.Common.Interfaces;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Academic.Queries.GetSubjects
{
    public class GetSubjectsQueryTest
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IMapper> _mockMapper;
        private readonly GetSubjectsQueryHandler _mockHandler;
        private readonly Mock<ISubjectRepository> _mockISubjectRepository;

        public GetSubjectsQueryTest()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockMapper = new Mock<IMapper>();
            _mockISubjectRepository = new Mock<ISubjectRepository>();

            // Mock UnitOfWork trả về SubjectRepository
            _mockUnitOfWork.Setup(u => u.GetRepository<ISubjectRepository>())
                        .Returns(_mockISubjectRepository.Object);

            // Khởi tạo handler
            _mockHandler = new GetSubjectsQueryHandler(_mockUnitOfWork.Object, _mockMapper.Object);
        }

        [Fact]
        public async Task Handle_SubjectListIsEmptyOrNull_ShouldThrowNotFoundException()
        {
            // 1. Arrange
            var query = new GetSubjectsQuery();
            _mockISubjectRepository.Setup(r => r.GetAllAsync())
                                    .ReturnsAsync(new List<Subject>());

            // 2. Act
            Func<Task> act = async () => await _mockHandler.Handle(query, CancellationToken.None);

            // 3. Assert
            await act.Should().ThrowAsync<NotFound>().WithMessage("Subject list is empty or was not found");
        }

        [Fact]
        public async Task Handle_ValidRequest_ShouldReturnMappedDefaultSubjectList()
        {
            // 1. Arrange
            var query = new GetSubjectsQuery();

            var subject = new Subject(
                subjectId: Guid.NewGuid(),
                code: "Test111",
                name: "TestSubject",
                gradeId: Guid.NewGuid()
            );

            var subjectList = new List<Subject> { subject };
            _mockISubjectRepository.Setup(r => r.GetAllAsync())
                                    .ReturnsAsync(subjectList);

            var subjectDTO = new SubjectDTO
            {
                SubjectID = subject.SubjectID,
                Code = subject.Code,
                Name = subject.Name,
                IsActive = subject.IsActive
            };
            var expectedDTOList = new List<SubjectDTO> { subjectDTO };

            _mockMapper
                .Setup(m => m.Map<IEnumerable<SubjectDTO>>(subjectList))
                .Returns(expectedDTOList);

            // 2. Act
            var result = await _mockHandler.Handle(query, CancellationToken.None);

            // 3. Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedDTOList);
        }
    }
}
