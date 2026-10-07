using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Options;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace UnitTests.InfrastructureTests
{
    public class PayOSPaymentServiceTests
    {
        private sealed class StubHandler : HttpMessageHandler
        {
            private readonly HttpStatusCode _status;
            private readonly string _body;

            public StubHandler(HttpStatusCode status, string body)
            {
                _status = status;
                _body = body;
            }

            public HttpRequestMessage? Request { get; private set; }
            public string? RequestBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Request = request;
                RequestBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(_status) { Content = new StringContent(_body) };
            }
        }

        private static readonly PayOSOptions Options = new()
        {
            ClientId = "client",
            ApiKey = "api-key",
            ChecksumKey = "checksum-key",
            ReturnUrl = "https://api.example.com/api/orders/return",
            CancelUrl = "https://app.example.com/student?payment=cancelled",
            FrontendUrl = "https://app.example.com"
        };

        private static PayOSPaymentService Create(StubHandler handler)
        {
            var factory = new Mock<IHttpClientFactory>();
            factory.Setup(f => f.CreateClient("PayOSClient")).Returns(new HttpClient(handler));
            return new PayOSPaymentService(factory.Object, Microsoft.Extensions.Options.Options.Create(Options));
        }

        [Fact]
        public async Task CreatesALinkWithASignedPayloadAndTheMerchantHeaders()
        {
            var handler = new StubHandler(HttpStatusCode.OK, """{"code":"00","desc":"success","data":{"checkoutUrl":"https://pay.payos.vn/web/abc"}}""");

            var url = await Create(handler).CreatePaymentLinkAsync(1791348072123456, 69999.6m, "ignored");

            url.Should().Be("https://pay.payos.vn/web/abc");
            handler.Request!.Headers.GetValues("x-client-id").Should().Equal("client");
            handler.Request.Headers.GetValues("x-api-key").Should().Equal("api-key");

            using var payload = JsonDocument.Parse(handler.RequestBody!);
            var root = payload.RootElement;

            // Whole VND, rounded the way FinishOrderCommand rounds when it compares what PayOS reports
            root.GetProperty("amount").GetInt32().Should().Be(70000);
            root.GetProperty("orderCode").GetInt64().Should().Be(1791348072123456);

            var description = root.GetProperty("description").GetString()!;
            description.Length.Should().BeLessThanOrEqualTo(25);

            var canonical = $"amount=70000&cancelUrl={Options.CancelUrl}&description={description}&orderCode=1791348072123456&returnUrl={Options.ReturnUrl}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Options.ChecksumKey));
            var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
            root.GetProperty("signature").GetString().Should().Be(expected);

            // The link stops working when the order is no longer payable
            root.GetProperty("expiredAt").GetInt64().Should().BeGreaterThan(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        }

        [Fact]
        public async Task AGatewayError_Throws()
        {
            var handler = new StubHandler(HttpStatusCode.BadRequest, """{"code":"20","desc":"bad request"}""");

            var act = () => Create(handler).CreatePaymentLinkAsync(1, 1000m, "x");

            (await act.Should().ThrowAsync<Exception>()).WithMessage("*PayOS Error*");
        }

        [Fact]
        public async Task AnAnswerWithoutACheckoutUrl_Throws()
        {
            var handler = new StubHandler(HttpStatusCode.OK, """{"code":"231","desc":"order exists","data":null}""");

            var act = () => Create(handler).CreatePaymentLinkAsync(1, 1000m, "x");

            await act.Should().ThrowAsync<Exception>();
        }
    }
}
