using System;
using Application.Features.Coupons.Commands.CreateCoupon;
using Application.Features.Courses.CreateCourse;
using Application.Features.Identity.Commands.Register;
using Application.Features.Statistics;
using Application.Features.Statistics.Queries.GetTopPerformance;
using Application.Features.Users.Commands;
using FluentAssertions;
using Xunit;

namespace UnitTests.Application.Validators
{
    /// <summary>Input rules added in the pre-frontend review.</summary>
    public class ReviewFindingsValidatorTests
    {
        private static CreateCourseCommand Course(decimal? price, string thumbnailName = "t.png", bool withFile = false) => new()
        {
            Title = "Algebra",
            Description = "About algebra",
            Price = price,
            ThumbnailName = thumbnailName,
            ThumbnailFileStream = withFile ? new System.IO.MemoryStream() : null
        };

        [Theory]
        [InlineData(null, true)]
        [InlineData("0", true)]
        [InlineData("15000", true)]
        [InlineData("15000.5", false)]
        [InlineData("-1", false)]
        [InlineData("1000000001", false)]
        public void CoursePrice_MustBeAWholeAmountThePaymentGatewayCanTake(string? price, bool valid)
        {
            var amount = price == null ? (decimal?)null : decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);

            new CreateCourseCommandValidator().Validate(Course(amount)).IsValid.Should().Be(valid);
        }

        [Fact]
        public void AThumbnailName_IsOnlyRequiredWhenNoFileIsSent()
        {
            var validator = new CreateCourseCommandValidator();

            validator.Validate(Course(1000, thumbnailName: "")).IsValid.Should().BeFalse();
            validator.Validate(Course(1000, thumbnailName: "", withFile: true)).IsValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("10000", true)]
        [InlineData("10000.5", false)]
        public void ACouponDiscount_MustBeAWholeAmount(string amount, bool valid)
        {
            var result = new CreateCouponCommandValidator().Validate(new CreateCouponCommand
            {
                Code = "SAVE10",
                DiscountAmount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture),
                StartDate = DateTime.UtcNow.AddDays(-1),
                ExpiredDate = DateTime.UtcNow.AddDays(30),
                MaxUsage = 5
            });

            result.IsValid.Should().Be(valid);
        }

        [Theory]
        [InlineData("0912345678", true)]
        [InlineData("", true)]        // blank means "leave as is"
        [InlineData(null, true)]
        [InlineData("12345", false)]
        [InlineData("abc", false)]
        public void AProfilePhone_FollowsTheRegistrationFormat(string? phone, bool valid)
        {
            new UpdateUserDetailsCommandValidator().Validate(new UpdateUserDetailsCommand { Phone = phone }).IsValid.Should().Be(valid);
        }

        [Fact]
        public void AProfileName_AndBio_AreLimited()
        {
            var validator = new UpdateUserDetailsCommandValidator();

            validator.Validate(new UpdateUserDetailsCommand { Name = new string('a', 101) }).IsValid.Should().BeFalse();
            validator.Validate(new UpdateUserDetailsCommand { Bio = new string('a', 1001) }).IsValid.Should().BeFalse();
            validator.Validate(new UpdateUserDetailsCommand { Name = new string('a', 100), Bio = new string('a', 1000) }).IsValid.Should().BeTrue();
        }

        [Fact]
        public void ARegistrationBio_IsLimitedToWhatTheColumnHolds()
        {
            var command = new RegisterCommand
            {
                Email = "a@b.com",
                Password = "Password123",
                Phone = "0912345678",
                Name = "A",
                Bio = new string('x', 1001)
            };

            new RegisterCommandValidator().Validate(command).IsValid.Should().BeFalse();
        }

        [Fact]
        public void TheTopOfARanking_IsBounded_AndADateRangeCannotRunBackwards()
        {
            var validator = new GetTopPerformanceQueryValidator();

            validator.Validate(new GetTopPerformanceQuery { Top = 0 }).IsValid.Should().BeFalse();
            validator.Validate(new GetTopPerformanceQuery { Top = 101 }).IsValid.Should().BeFalse();
            validator.Validate(new GetTopPerformanceQuery { Top = 10 }).IsValid.Should().BeTrue();
            validator.Validate(new GetTopPerformanceQuery { Top = 10, From = new DateTime(2026, 2, 1), To = new DateTime(2026, 1, 1) }).IsValid.Should().BeFalse();
        }
    }
}
