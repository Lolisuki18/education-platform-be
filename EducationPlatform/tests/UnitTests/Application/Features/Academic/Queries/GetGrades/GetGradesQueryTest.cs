using Application.Exceptions;
using Application.Features.Academic.Queries.GetGrades;
using Application.Results;
using AutoMapper;
using Domain.AcademicManagement.Aggregate;
using Domain.Common.Interfaces;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Text;

namespace UnitTests.Application.Features.Academic.Queries.GetGrades
{
    public class GetGradesQueryTest
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IMapper> _mockMapper;
        private readonly GetGradesQueryHandler _mockHandler;
        private readonly Mock<IGradeRepository> _mockIGradeRepository;

        public GetGradesQueryTest()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockMapper = new Mock<IMapper>();
            _mockIGradeRepository = new Mock<IGradeRepository>();
            // Mock UnitOfWork trả về GradeRepository
            _mockUnitOfWork.Setup(u => u.GetRepository<IGradeRepository>())
                        .Returns(_mockIGradeRepository.Object);
            //Khởi tạo handler
            _mockHandler = new GetGradesQueryHandler(_mockUnitOfWork.Object, _mockMapper.Object);
        }

        [Fact]
        public async Task Handle_GradeListIsEmpty_ShouldReturnAnEmptyList()
        {
            //1.Arrange : prepare for query
            var query = new GetGradesQuery();
            //simulate the repository to return an empty list for grades
            _mockIGradeRepository.Setup(r => r.GetAllAsync())
                                    .ReturnsAsync(new List<Grade>());
            //2.Act: call the handler to handle the query
            var result = await _mockHandler.Handle(query, CancellationToken.None);
            // 3. Assert: no grades is an empty list, not an error
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_ValidRequest_ShouldReturnMappedGradesList()
        {
            // 1. Arrange: Chuẩn bị dữ liệu
            var query = new GetGradesQuery();
            // Khởi tạo đối tượng Grade thật để đưa vào danh sách trả về
            var grade = new Grade(
                gradeId: Guid.NewGuid(),
                name: "Grade 1"
            );
            var gradeList = new List<Grade> { grade };
            // Cài đặt cho Mock Repository trả về danh sách vừa tạo
            _mockIGradeRepository
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(gradeList);
            // Giả lập DTO đầu ra sau khi Mapper thực hiện map dữ liệu
            var gradeDTO = new GradeDTO
            {
                GradeID = grade.GradeID,
                Name = grade.Name
            };
            var expectedDTOList = new List<GradeDTO> { gradeDTO };
            _mockMapper.Setup(m => m.Map<IEnumerable<GradeDTO>>(gradeList)).Returns(expectedDTOList);
            // 2. Act: call the handler to handle the query
            var result = await _mockHandler.Handle(query, CancellationToken.None);
            // 3. Assert: verify that the result is as expected
            result.Should().BeEquivalentTo(expectedDTOList);
        }

    }
}
