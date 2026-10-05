using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Infrastructure.Services;
using Xunit;

namespace UnitTests.InfrastructureTests
{
    public class PayOSSignatureVerifierTests
    {
        private const string Key = "checksum-key";
        private readonly PayOSSignatureVerifier _verifier = new();

        private static string Sign(string raw)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Key));
            return System.Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        }

        [Fact]
        public void Webhook_ValidSignature_ShouldPass_RegardlessOfFieldOrder()
        {
            var data = new Dictionary<string, string?>
            {
                ["orderCode"] = "123",
                ["amount"] = "3000",
                ["description"] = "VQRIO123"
            };

            // Fields are signed in alphabetical order
            var signature = Sign("amount=3000&description=VQRIO123&orderCode=123");

            _verifier.VerifyWebhookSignature(data, signature, Key).Should().BeTrue();
        }

        [Fact]
        public void Webhook_SignatureIsCaseInsensitiveHex()
        {
            var data = new Dictionary<string, string?> { ["orderCode"] = "123" };
            var signature = Sign("orderCode=123").ToUpperInvariant();

            _verifier.VerifyWebhookSignature(data, signature, Key).Should().BeTrue();
        }

        [Fact]
        public void Webhook_TamperedAmount_ShouldFail()
        {
            var signature = Sign("amount=3000&orderCode=123");
            var tampered = new Dictionary<string, string?> { ["orderCode"] = "123", ["amount"] = "1" };

            _verifier.VerifyWebhookSignature(tampered, signature, Key).Should().BeFalse();
        }

        [Fact]
        public void Webhook_WrongKey_ShouldFail()
        {
            var data = new Dictionary<string, string?> { ["orderCode"] = "123" };

            _verifier.VerifyWebhookSignature(data, Sign("orderCode=123"), "another-key").Should().BeFalse();
        }

        [Fact]
        public void Webhook_EmptySignature_ShouldFail()
        {
            _verifier.VerifyWebhookSignature(new Dictionary<string, string?> { ["orderCode"] = "123" }, "", Key).Should().BeFalse();
        }

        [Fact]
        public void Redirect_ValidSignature_ShouldPass()
        {
            var signature = Sign("cancel=false&code=00&id=link-id&orderCode=123&status=PAID");

            _verifier.VerifyRedirectSignature("", "false", "00", "link-id", "123", "PAID", signature, Key).Should().BeTrue();
        }

        [Fact]
        public void Redirect_ChangedStatus_ShouldFail()
        {
            var signature = Sign("cancel=false&code=00&id=link-id&orderCode=123&status=CANCELLED");

            _verifier.VerifyRedirectSignature("", "false", "00", "link-id", "123", "PAID", signature, Key).Should().BeFalse();
        }
    }
}
