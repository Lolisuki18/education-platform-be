using Application.Interface;
using Domain.AuditManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using Infrastructure.Implementation;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace UnitTests.InfrastructureTests
{
    /// <summary>What the API caches about an account must be dropped by whichever handler changed it.</summary>
    public class UserStatusInvalidationTests
    {
        private readonly Mock<IUserActivityCache> _cache = new();

        private (UnitOfWork Uow, EducationPlatformDBContext Db) Create()
        {
            var db = new EducationPlatformDBContext(
                new DbContextOptionsBuilder<EducationPlatformDBContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

            var services = new ServiceCollection();
            services.AddSingleton(db);
            services.AddScoped<IAuditRepository, AuditLogRepository>();
            services.AddSingleton(_cache.Object);

            return (new UnitOfWork(db, services.BuildServiceProvider(), new AfterCommitQueue(NullLogger<AfterCommitQueue>.Instance)), db);
        }

        private static User NewUser() =>
            new(Guid.NewGuid(), "u@example.com", "Secret123", "0123456789", "User", null, Role.Student, DateTime.UtcNow, true);

        [Fact]
        public async Task DeactivatingAUser_DropsTheirCachedStatus_AfterTheCommit()
        {
            var (uow, db) = Create();
            var user = NewUser();
            db.Users.Add(user);
            await uow.CommitAsync();
            _cache.Invocations.Clear();

            user.Deactivate();
            await uow.CommitAsync();

            _cache.Verify(c => c.Invalidate(user.UserID), Times.Once);
        }

        [Fact]
        public async Task EndingAllSessions_DropsTheCachedStatus_SoTheNewCutoffIsSeenAtOnce()
        {
            var (uow, db) = Create();
            var user = NewUser();
            db.Users.Add(user);
            await uow.CommitAsync();
            _cache.Invocations.Clear();

            user.RevokeAllRefreshTokens();
            await uow.CommitAsync();

            _cache.Verify(c => c.Invalidate(user.UserID), Times.Once);
        }

        [Fact]
        public async Task ChangingTheRole_DropsTheCachedStatus()
        {
            var (uow, db) = Create();
            var user = NewUser();
            db.Users.Add(user);
            await uow.CommitAsync();
            _cache.Invocations.Clear();

            user.ChangeRole(Role.Teacher);
            await uow.CommitAsync();

            _cache.Verify(c => c.Invalidate(user.UserID), Times.Once);
        }

        [Fact]
        public async Task EditingTheProfile_LeavesTheCacheAlone()
        {
            var (uow, db) = Create();
            var user = NewUser();
            db.Users.Add(user);
            await uow.CommitAsync();
            _cache.Invocations.Clear();

            user.UpdateProfile("New Name", null, null);
            await uow.CommitAsync();

            _cache.Verify(c => c.Invalidate(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task ANewUser_NeedsNothingInvalidated()
        {
            var (uow, db) = Create();
            db.Users.Add(NewUser());

            await uow.CommitAsync();

            _cache.Verify(c => c.Invalidate(It.IsAny<Guid>()), Times.Never);
        }
    }
}
