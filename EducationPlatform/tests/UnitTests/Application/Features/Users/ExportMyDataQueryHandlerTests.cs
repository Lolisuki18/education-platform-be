using Application.Exceptions;
using Application.Features.Users.Queries;
using Application.Interface;
using Application.Results;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace UnitTests.Application.Features.Users
{
    public class ExportMyDataQueryHandlerTests
    {
        private readonly Mock<IPersonalDataReader> _reader = new();
        private readonly Mock<ICurrentUser> _currentUser = new();
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));
        private readonly ExportMyDataQueryHandler _handler;

        public ExportMyDataQueryHandlerTests()
        {
            _handler = new ExportMyDataQueryHandler(_reader.Object, _currentUser.Object, _time);
        }

        [Fact]
        public async Task Handle_ReturnsTheCallersOwnDataStampedWithTheExportTime()
        {
            var userId = Guid.NewGuid();
            _currentUser.Setup(c => c.Id).Returns(userId);
            _reader.Setup(r => r.ReadAsync(userId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PersonalDataExport { Profile = new ExportedProfile { UserID = userId } });

            var result = await _handler.Handle(new ExportMyDataQuery(), CancellationToken.None);

            result.Profile.UserID.Should().Be(userId);
            result.ExportedAt.Should().Be(_time.GetUtcNow().UtcDateTime);
        }

        [Fact]
        public async Task Handle_WithoutSignIn_IsRefused()
        {
            _currentUser.Setup(c => c.Id).Returns((Guid?)null);

            var act = () => _handler.Handle(new ExportMyDataQuery(), CancellationToken.None);

            await act.Should().ThrowAsync<AuthenticateException>();
            _reader.Verify(r => r.ReadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_UnknownUser_IsNotFound()
        {
            _currentUser.Setup(c => c.Id).Returns(Guid.NewGuid());
            _reader.Setup(r => r.ReadAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PersonalDataExport?)null);

            var act = () => _handler.Handle(new ExportMyDataQuery(), CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>();
        }
    }
}
