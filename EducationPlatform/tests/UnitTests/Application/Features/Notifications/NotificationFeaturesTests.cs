using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.Exceptions;
using Application.Features.Notifications;
using Application.Interface;
using Domain.Common.Interfaces;
using Domain.Exceptions;
using Domain.NotificationManagement.Aggregate;
using FluentAssertions;
using Moq;
using Xunit;

namespace UnitTests.Application.Features.Notifications
{
    public class NotificationDomainTests
    {
        [Fact]
        public void NewNotification_StartsUnread()
        {
            var n = new Notification(Guid.NewGuid(), "Title", "Message");

            n.IsRead.Should().BeFalse();
            n.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void MarkAsRead_KeepsTheFirstReadTime()
        {
            var n = new Notification(Guid.NewGuid(), "Title", "Message");

            n.MarkAsRead();
            var first = n.ReadAt;
            n.MarkAsRead();

            n.IsRead.Should().BeTrue();
            n.ReadAt.Should().Be(first);
        }

        [Fact]
        public void LongTextIsTruncatedToTheColumnSize()
        {
            var n = new Notification(Guid.NewGuid(), new string('t', 500), new string('m', 5000));

            n.Title.Length.Should().Be(Notification.MaxTitleLength);
            n.Message.Length.Should().Be(Notification.MaxMessageLength);
        }

        [Fact]
        public void ATitleAndAUserAreRequired()
        {
            Action noUser = () => new Notification(Guid.Empty, "Title", "Message");
            Action noTitle = () => new Notification(Guid.NewGuid(), " ", "Message");

            noUser.Should().Throw<DomainException>();
            noTitle.Should().Throw<DomainException>();
        }
    }

    public class NotificationHandlerTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWork = new();
        private readonly Mock<INotificationRepository> _repository = new();
        private readonly Mock<ICurrentUser> _currentUser = new();
        private readonly Guid _me = Guid.NewGuid();

        public NotificationHandlerTests()
        {
            _unitOfWork.Setup(u => u.GetRepository<INotificationRepository>()).Returns(_repository.Object);
            _currentUser.Setup(u => u.Id).Returns(_me);
        }

        [Fact]
        public async Task List_ReturnsMyNotificationsWithPaging()
        {
            var mine = new Notification(_me, "Hello", "World");
            _repository
                .Setup(r => r.GetForUserAsync(_me, true, 2, 5, It.IsAny<CancellationToken>()))
                .ReturnsAsync((new List<Notification> { mine }, 11));

            var result = await new GetNotificationsQueryHandler(_unitOfWork.Object, _currentUser.Object)
                .Handle(new GetNotificationsQuery { UnreadOnly = true, PageIndex = 2, PageSize = 5 }, CancellationToken.None);

            result.TotalItems.Should().Be(11);
            result.PageIndex.Should().Be(2);
            result.Items.Should().ContainSingle().Which.Title.Should().Be("Hello");
        }

        [Fact]
        public void PagingIsClamped()
        {
            var query = new GetNotificationsQuery { PageIndex = 0, PageSize = 100000 };

            query.PageIndex.Should().Be(1);
            query.PageSize.Should().Be(100);
        }

        [Fact]
        public async Task List_RequiresASignedInUser()
        {
            _currentUser.Setup(u => u.Id).Returns((Guid?)null);

            Func<Task> act = () => new GetNotificationsQueryHandler(_unitOfWork.Object, _currentUser.Object)
                .Handle(new GetNotificationsQuery(), CancellationToken.None);

            await act.Should().ThrowAsync<AuthenticateException>();
        }

        [Fact]
        public async Task UnreadCount_CountsForTheCurrentUserOnly()
        {
            _repository.Setup(r => r.CountUnreadAsync(_me, It.IsAny<CancellationToken>())).ReturnsAsync(7);

            var count = await new GetUnreadNotificationCountQueryHandler(_unitOfWork.Object, _currentUser.Object)
                .Handle(new GetUnreadNotificationCountQuery(), CancellationToken.None);

            count.Should().Be(7);
        }

        [Fact]
        public async Task MarkRead_MarksMyNotificationAndSaves()
        {
            var mine = new Notification(_me, "Hello", "World");
            _repository.Setup(r => r.GetByIdAsync(mine.NotificationID, It.IsAny<CancellationToken>())).ReturnsAsync(mine);

            await new MarkNotificationReadCommandHandler(_unitOfWork.Object, _currentUser.Object)
                .Handle(new MarkNotificationReadCommand { NotificationId = mine.NotificationID }, CancellationToken.None);

            mine.IsRead.Should().BeTrue();
            _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<string?>()), Times.Once);
        }

        [Fact]
        public async Task MarkRead_OfSomeoneElsesNotification_LooksLikeItDoesNotExist()
        {
            var theirs = new Notification(Guid.NewGuid(), "Private", "Message");
            _repository.Setup(r => r.GetByIdAsync(theirs.NotificationID, It.IsAny<CancellationToken>())).ReturnsAsync(theirs);

            Func<Task> act = () => new MarkNotificationReadCommandHandler(_unitOfWork.Object, _currentUser.Object)
                .Handle(new MarkNotificationReadCommand { NotificationId = theirs.NotificationID }, CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>();
            theirs.IsRead.Should().BeFalse();
        }

        [Fact]
        public async Task MarkRead_OfAnUnknownNotification_IsNotFound()
        {
            Func<Task> act = () => new MarkNotificationReadCommandHandler(_unitOfWork.Object, _currentUser.Object)
                .Handle(new MarkNotificationReadCommand { NotificationId = Guid.NewGuid() }, CancellationToken.None);

            await act.Should().ThrowAsync<NotFoundException>();
        }

        [Fact]
        public async Task MarkAll_ReturnsHowManyChanged()
        {
            _repository.Setup(r => r.MarkAllAsReadAsync(_me, It.IsAny<CancellationToken>())).ReturnsAsync(4);

            var changed = await new MarkAllNotificationsReadCommandHandler(_unitOfWork.Object, _currentUser.Object)
                .Handle(new MarkAllNotificationsReadCommand(), CancellationToken.None);

            changed.Should().Be(4);
        }
    }
}
