using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace IntegrationTests.Controllers
{
    public class SecurityHeadersTests : IntegrationTestBase
    {
        public SecurityHeadersTests(CustomWebApplicationFactory factory) : base(factory)
        {
        }

        [Fact]
        public async Task ApiResponses_CarryTheHardeningHeaders_EvenWhenTheyAreErrors()
        {
            // No sign-in: a 401 must be hardened too
            var response = await Client.GetAsync("/api/user/me");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
            response.Headers.GetValues("X-Frame-Options").Should().ContainSingle().Which.Should().Be("DENY");
            response.Headers.GetValues("Referrer-Policy").Should().ContainSingle().Which.Should().Be("no-referrer");
            response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle().Which.Should().Contain("default-src 'none'");
        }

        [Fact]
        public async Task TheHealthEndpoint_IsHardenedToo_ButHasNoApiOnlyPolicy()
        {
            var response = await Client.GetAsync("/healthz");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Headers.Contains("X-Content-Type-Options").Should().BeTrue();
            response.Headers.Contains("Content-Security-Policy").Should().BeFalse();
        }

        [Fact]
        public async Task TheServerDoesNotAnnounceItsSoftware()
        {
            var response = await Client.GetAsync("/healthz");

            response.Headers.Contains("Server").Should().BeFalse();
            response.Headers.Contains("X-Powered-By").Should().BeFalse();
        }
    }
}
