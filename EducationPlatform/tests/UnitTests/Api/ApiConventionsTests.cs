using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using API.Middlewares;
using API.Models.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace UnitTests.ApiTests
{
    public class ApiConventionsTests
    {
        private static readonly Type[] Controllers = typeof(API.Controllers.AuthController).Assembly
            .GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .ToArray();

        [Fact]
        public void EveryEndpointStatesItsAccessRulesExplicitly()
        {
            // The fallback policy would protect a forgotten endpoint, but a deliberate decision per endpoint
            // (Authorize or AllowAnonymous) keeps the public surface of the API reviewable.
            var undecided = Controllers
                .SelectMany(c => c.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => m.GetCustomAttributes().Any(a => a is HttpMethodAttribute))
                    .Select(m => (Controller: c, Action: m)))
                .Where(x =>
                    !x.Controller.IsDefined(typeof(AuthorizeAttribute), true) &&
                    !x.Controller.IsDefined(typeof(AllowAnonymousAttribute), true) &&
                    !x.Action.IsDefined(typeof(AuthorizeAttribute), true) &&
                    !x.Action.IsDefined(typeof(AllowAnonymousAttribute), true))
                .Select(x => $"{x.Controller.Name}.{x.Action.Name}")
                .ToList();

            undecided.Should().BeEmpty("every endpoint needs [Authorize] or [AllowAnonymous]");
        }

        [Fact]
        public void EveryControllerIsReachableThroughTheVersionedRoute()
        {
            var missing = Controllers
                .Where(c => !c.GetCustomAttributes<RouteAttribute>().Any(r => r.Template.Contains("v{version:apiVersion}")))
                .Select(c => c.Name)
                .ToList();

            missing.Should().BeEmpty();
        }

        [Theory]
        [InlineData("abc-123", "abc-123")]
        [InlineData("0HNP2SB5LGF38:00000001", "0HNP2SB5LGF38:00000001")]
        [InlineData("a.b_c-D9", "a.b_c-D9")]
        public void CorrelationId_AcceptsPlainIdentifiers(string supplied, string expected)
        {
            CorrelationIdMiddleware.Resolve(supplied, "server-id").Should().Be(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("line1\r\nINFO forged log entry")]
        [InlineData("has space")]
        [InlineData("<script>")]
        [InlineData("café")]
        public void CorrelationId_ReplacesAnythingElseWithTheServerGeneratedId(string? supplied)
        {
            CorrelationIdMiddleware.Resolve(supplied, "server-id").Should().Be("server-id");
        }

        [Fact]
        public void CorrelationId_RejectsOverlongValues()
        {
            CorrelationIdMiddleware.Resolve(new string('a', 65), "server-id").Should().Be("server-id");
            CorrelationIdMiddleware.Resolve(new string('a', 64), "server-id").Should().HaveLength(64);
        }

        [Fact]
        public void RefreshTokenRequest_AcceptsAnObject()
        {
            var dto = JsonSerializer.Deserialize<RefreshTokenRequestDto>("{\"refreshToken\":\"abc\"}");

            dto!.RefreshToken.Should().Be("abc");
        }

        [Fact]
        public void RefreshTokenRequest_StillAcceptsABareString()
        {
            var dto = JsonSerializer.Deserialize<RefreshTokenRequestDto>("\"abc\"");

            dto!.RefreshToken.Should().Be("abc");
        }

        [Fact]
        public void RefreshTokenRequest_IsCaseInsensitiveAboutTheName()
        {
            JsonSerializer.Deserialize<RefreshTokenRequestDto>("{\"RefreshToken\":\"abc\"}")!.RefreshToken.Should().Be("abc");
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("{\"refreshToken\":5}")]
        public void RefreshTokenRequest_WithoutAUsableToken_ComesOutEmptySoValidationRejectsIt(string json)
        {
            JsonSerializer.Deserialize<RefreshTokenRequestDto>(json)!.RefreshToken.Should().BeEmpty();
        }

        [Fact]
        public void RefreshTokenRequest_RejectsOtherJsonShapes()
        {
            Action act = () => JsonSerializer.Deserialize<RefreshTokenRequestDto>("[1,2]");

            act.Should().Throw<JsonException>();
        }
    }
}
