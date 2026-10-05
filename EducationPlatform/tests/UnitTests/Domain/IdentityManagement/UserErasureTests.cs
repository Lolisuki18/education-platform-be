using Domain.Exceptions;
using Domain.IdentityManagement.Aggregate;
using Domain.IdentityManagement.Enum;
using FluentAssertions;
using Xunit;

namespace UnitTests.DomainTests.IdentityManagement
{
    public class UserErasureTests
    {
        private static User CreateUser(Role role = Role.Student)
            => new(Guid.NewGuid(), "person@example.com", "Secret123", "0901234567", "Real Name", "About me", role, DateTime.UtcNow, isVerified: true);

        [Fact]
        public void Erase_RemovesEverythingThatIdentifiesThePerson()
        {
            var user = CreateUser();
            user.GenerateEmailOtp(TimeSpan.FromMinutes(5));

            user.Erase(DateTime.UtcNow);

            user.Email.Should().NotContain("person").And.EndWith("@deleted.invalid");
            user.Phone.Should().NotBe("0901234567");
            user.Name.Should().Be(User.DeletedName);
            user.Bio.Should().BeNull();
            user.EmailOtp.Should().BeNull();
            user.EmailOtpExpiresAt.Should().BeNull();
            user.IsDeleted.Should().BeTrue();
            user.DeletedAt.Should().NotBeNull();
            user.IsActive.Should().BeFalse();
        }

        [Fact]
        public void Erase_KeepsThePhoneWithinTheColumnLength()
        {
            var user = CreateUser();

            user.Erase(DateTime.UtcNow);

            user.Phone.Length.Should().BeLessThanOrEqualTo(20);
            user.Email.Length.Should().BeLessThanOrEqualTo(200);
        }

        [Fact]
        public void Erase_GivesDifferentUsersDifferentPlaceholders_SoTheUniqueIndexesHold()
        {
            var first = CreateUser();
            var second = CreateUser();

            first.Erase(DateTime.UtcNow);
            second.Erase(DateTime.UtcNow);

            first.Email.Should().NotBe(second.Email);
            first.Phone.Should().NotBe(second.Phone);
        }

        [Fact]
        public void Erase_MakesTheOldPasswordUseless()
        {
            var user = CreateUser();

            user.Erase(DateTime.UtcNow);

            user.VerifyLogin("Secret123").Should().BeFalse();
        }

        [Fact]
        public void Erase_RevokesEverySession()
        {
            var user = CreateUser();
            user.IssueRefreshToken("token-one", TimeSpan.FromDays(7));
            user.IssueRefreshToken("token-two", TimeSpan.FromDays(7));

            user.Erase(DateTime.UtcNow);

            user.CanRefresh("token-one").Should().BeFalse();
            user.CanRefresh("token-two").Should().BeFalse();
        }

        [Fact]
        public void Erase_Twice_Throws()
        {
            var user = CreateUser();
            user.Erase(DateTime.UtcNow);

            var act = () => user.Erase(DateTime.UtcNow);

            act.Should().Throw<DomainException>();
        }

        [Fact]
        public void Activate_AfterErasure_Throws()
        {
            var user = CreateUser();
            user.Erase(DateTime.UtcNow);

            var act = () => user.Activate();

            act.Should().Throw<DomainException>();
            user.IsActive.Should().BeFalse();
        }
    }
}
