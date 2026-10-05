using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using API.Helpers;
using FluentAssertions;
using Xunit;

namespace UnitTests.ApiTests
{
    public class HealthProbeTests
    {
        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
            public Uri? LastRequest { get; private set; }

            public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request.RequestUri;
                return Task.FromResult(_respond(request));
            }
        }

        [Theory]
        [InlineData("http://+:8080", "http://localhost:8080/healthz")]
        [InlineData("http://*:5000", "http://localhost:5000/healthz")]
        [InlineData("http://0.0.0.0:9000", "http://localhost:9000/healthz")]
        [InlineData("https://localhost:7029;http://localhost:5287", "https://localhost:7029/healthz")]
        [InlineData("http://myhost:81/some/path", "http://myhost:81/healthz")]
        [InlineData(null, "http://localhost:8080/healthz")]
        [InlineData("", "http://localhost:8080/healthz")]
        [InlineData("not a url", "http://localhost:8080/healthz")]
        public void ResolveHealthUrl_TargetsTheLocalInstance(string? urls, string expected)
        {
            HealthProbe.ResolveHealthUrl(urls).ToString().Should().Be(expected);
        }

        [Fact]
        public async Task HealthyInstance_ExitsWithZero()
        {
            var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

            var exitCode = await HealthProbe.RunAsync("http://+:8080", handler);

            exitCode.Should().Be(0);
            handler.LastRequest!.ToString().Should().Be("http://localhost:8080/healthz");
        }

        [Fact]
        public async Task UnhealthyInstance_ExitsWithOne()
        {
            var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

            (await HealthProbe.RunAsync("http://+:8080", handler)).Should().Be(1);
        }

        [Fact]
        public async Task UnreachableInstance_ExitsWithOneInsteadOfThrowing()
        {
            var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));

            (await HealthProbe.RunAsync("http://+:8080", handler)).Should().Be(1);
        }
    }
}
