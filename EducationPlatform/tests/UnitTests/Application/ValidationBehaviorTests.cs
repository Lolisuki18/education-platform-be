using Application.Common.Behaviors;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Xunit;

namespace UnitTests.Application
{
    public class ValidationBehaviorTests
    {
        public record Ping(string Name) : IRequest<string>;

        private sealed class NameRequired : AbstractValidator<Ping>
        {
            public NameRequired() => RuleFor(p => p.Name).NotEmpty().WithMessage("Name is required.");
        }

        private sealed class NameShort : AbstractValidator<Ping>
        {
            public NameShort() => RuleFor(p => p.Name).MaximumLength(3).WithMessage("Name is too long.");
        }

        private static Task<string> Next() => Task.FromResult("handled");

        [Fact]
        public async Task ValidRequest_ReachesTheHandler()
        {
            var behavior = new ValidationBehavior<Ping, string>(new IValidator<Ping>[] { new NameRequired() });

            var result = await behavior.Handle(new Ping("Ann"), Next, CancellationToken.None);

            result.Should().Be("handled");
        }

        [Fact]
        public async Task InvalidRequest_NeverReachesTheHandler()
        {
            var called = false;
            var behavior = new ValidationBehavior<Ping, string>(new IValidator<Ping>[] { new NameRequired() });

            var act = () => behavior.Handle(new Ping(""), () => { called = true; return Next(); }, CancellationToken.None);

            var thrown = await act.Should().ThrowAsync<ValidationException>();
            thrown.Which.Errors.Should().ContainSingle(e => e.ErrorMessage == "Name is required.");
            called.Should().BeFalse();
        }

        [Fact]
        public async Task FailuresOfEveryValidator_AreReportedTogether()
        {
            var behavior = new ValidationBehavior<Ping, string>(new IValidator<Ping>[] { new NameRequired(), new NameShort() });

            // Empty: only the first rule fails; too long: only the second one. Both must be visible in one answer
            var empty = await FluentActions.Awaiting(() => behavior.Handle(new Ping(""), Next, CancellationToken.None))
                .Should().ThrowAsync<ValidationException>();
            var tooLong = await FluentActions.Awaiting(() => behavior.Handle(new Ping("Annabelle"), Next, CancellationToken.None))
                .Should().ThrowAsync<ValidationException>();

            empty.Which.Errors.Select(e => e.ErrorMessage).Should().Equal("Name is required.");
            tooLong.Which.Errors.Select(e => e.ErrorMessage).Should().Equal("Name is too long.");

            var both = new ValidationBehavior<Ping, string>(new IValidator<Ping>[]
            {
                new NameRequired(),
                new InlineValidator<Ping> { v => v.RuleFor(p => p.Name).Must(_ => false).WithMessage("Second rule.") }
            });
            var bothThrown = await FluentActions.Awaiting(() => both.Handle(new Ping(""), Next, CancellationToken.None))
                .Should().ThrowAsync<ValidationException>();
            bothThrown.Which.Errors.Select(e => e.ErrorMessage).Should().BeEquivalentTo("Name is required.", "Second rule.");
        }

        [Fact]
        public async Task WithoutValidators_TheRequestPassesThrough()
        {
            var behavior = new ValidationBehavior<Ping, string>(Array.Empty<IValidator<Ping>>());

            (await behavior.Handle(new Ping(""), Next, CancellationToken.None)).Should().Be("handled");
        }
    }
}
