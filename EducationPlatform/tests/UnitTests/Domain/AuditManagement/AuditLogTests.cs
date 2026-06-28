using Domain.AuditManagement.Aggregate;
using FluentAssertions;
using System;
using Xunit;

namespace UnitTests.DomainTests.AuditManagement
{
    public class AuditLogTests
    {
        [Fact]
        public void AuditLogConstructor_ShouldInitializePropertiesCorrectly()
        {
            var auditLog = new AuditLog(
                "Course",
                "Create",
                "AdminUser",
                "{}",
                "{\"Title\":\"New Course\"}"
            );

            auditLog.AuditLogId.Should().NotBeEmpty();
            auditLog.EntityName.Should().Be("Course");
            auditLog.Action.Should().Be("Create");
            auditLog.PerformedBy.Should().Be("AdminUser");
            auditLog.OldValue.Should().Be("{}");
            auditLog.NewValue.Should().Be("{\"Title\":\"New Course\"}");
            auditLog.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }
    }
}
