using Application.Features.Courses.Queries.GetPolicies;
using Application.Results;
using AutoMapper;
using Domain.Common.Interfaces;
using Domain.CourseManagement.Aggregate;
using Domain.CourseManagement.Entity;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace UnitTests.Application.Features.Courses.Queries.GetPolicies
{
    public class GetPoliciesQueryHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IPolicyRepository> _mockPolicyRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly GetPoliciesQueryHandler _handler;

        public GetPoliciesQueryHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockPolicyRepository = new Mock<IPolicyRepository>();
            _mockMapper = new Mock<IMapper>();

            _mockUnitOfWork
                .Setup(u => u.GetRepository<IPolicyRepository>())
                .Returns(_mockPolicyRepository.Object);

            _handler = new GetPoliciesQueryHandler(_mockUnitOfWork.Object, _mockMapper.Object);
        }

        [Fact]
        public async Task Handle_ActiveOnlyIsTrue_ShouldReturnOnlyActivePolicyRules()
        {
            // Arrange
            var activeRule = new PolicyRule(Guid.NewGuid(), "RULE01", "Active Rule", Guid.NewGuid());

            var inactiveRule = new PolicyRule(Guid.NewGuid(), "RULE02", "Inactive Rule", Guid.NewGuid());
            SetIsActive(inactiveRule, false);

            var rulesList = new List<PolicyRule> { activeRule, inactiveRule };

            _mockPolicyRepository
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(rulesList);

            var expectedDtos = new List<PolicyDTO>
            {
                new PolicyDTO { PolicyID = activeRule.PolicyID, Name = "Active Policy" }
            };

            // Thiết lập mock mapper nhận vào danh sách chỉ chứa activeRule
            _mockMapper
                .Setup(m => m.Map<IEnumerable<PolicyDTO>>(It.Is<IEnumerable<PolicyRule>>(list =>
                    list.Count() == 1 && list.Contains(activeRule) && !list.Contains(inactiveRule)
                )))
                .Returns(expectedDtos);

            var query = new GetPoliciesQuery { ActiveOnly = true };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().BeEquivalentTo(expectedDtos);
            _mockPolicyRepository.Verify(r => r.GetAllAsync(), Times.Once);
        }

        [Fact]
        public async Task Handle_ActiveOnlyIsFalse_ShouldReturnAllPolicyRules()
        {
            // Arrange
            var activeRule = new PolicyRule(Guid.NewGuid(), "RULE01", "Active Rule", Guid.NewGuid());

            var inactiveRule = new PolicyRule(Guid.NewGuid(), "RULE02", "Inactive Rule", Guid.NewGuid());
            SetIsActive(inactiveRule, false);

            var rulesList = new List<PolicyRule> { activeRule, inactiveRule };

            _mockPolicyRepository
                .Setup(r => r.GetAllAsync())
                .ReturnsAsync(rulesList);

            var expectedDtos = new List<PolicyDTO>
            {
                new PolicyDTO { PolicyID = activeRule.PolicyID, Name = "Active Policy" },
                new PolicyDTO { PolicyID = inactiveRule.PolicyID, Name = "Inactive Policy" }
            };

            // Thiết lập mock mapper nhận vào toàn bộ danh sách rulesList
            _mockMapper
                .Setup(m => m.Map<IEnumerable<PolicyDTO>>(It.Is<IEnumerable<PolicyRule>>(list =>
                    list.Count() == 2 && list.Contains(activeRule) && list.Contains(inactiveRule)
                )))
                .Returns(expectedDtos);

            var query = new GetPoliciesQuery { ActiveOnly = false };

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().BeEquivalentTo(expectedDtos);
            _mockPolicyRepository.Verify(r => r.GetAllAsync(), Times.Once);
        }

        private void SetIsActive(PolicyRule target, bool value)
        {
            var prop = target.GetType().GetProperty(nameof(PolicyRule.IsActive), BindingFlags.Public | BindingFlags.Instance);
            prop?.SetValue(target, value);
        }
    }
}
