using Application.Common;
using Application.Features.Courses.Queries.GetLandingPage;
using FluentAssertions;
using Xunit;

namespace UnitTests.Application
{
    public class PagingTests
    {
        [Theory]
        [InlineData(0, 10)]
        [InlineData(-5, 10)]
        [InlineData(1, 1)]
        [InlineData(100, 100)]
        [InlineData(101, 100)]
        [InlineData(1_000_000, 100)]
        public void NormalizePageSize_ShouldStayWithinBounds(int requested, int expected)
        {
            Paging.NormalizePageSize(requested).Should().Be(expected);
        }

        [Theory]
        [InlineData(-3, 1)]
        [InlineData(0, 1)]
        [InlineData(7, 7)]
        public void NormalizePageIndex_ShouldNeverBeBelowOne(int requested, int expected)
        {
            Paging.NormalizePageIndex(requested).Should().Be(expected);
        }

        [Fact]
        public void ClientSuppliedPageSize_ShouldBeClampedOnTheQueryItself()
        {
            var query = new GetLandingPageQuery { PageIndex = -1, PageSize = 5000 };

            query.PageIndex.Should().Be(1);
            query.PageSize.Should().Be(Paging.MaxPageSize);
        }
    }
}
