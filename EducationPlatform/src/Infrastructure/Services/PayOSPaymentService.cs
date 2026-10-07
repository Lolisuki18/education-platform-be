using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Interface;
using Application.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services
{
    public class PayOSPaymentService : IPaymentService
    {
        private const string PaymentRequestsUrl = "https://api-merchant.payos.vn/v2/payment-requests";

        private readonly HttpClient _httpClient;
        private readonly PayOSOptions _options;

        public PayOSPaymentService(IHttpClientFactory factory, IOptions<PayOSOptions> options)
        {
            _httpClient = factory.CreateClient("PayOSClient");
            _options = options.Value;
        }

        public async Task<string> CreatePaymentLinkAsync(long orderCode, decimal amount, string description)
        {
            // PayOS takes whole VND; FinishOrderCommand compares what it reports with the same rounding
            int intAmount = checked((int)Math.Round(amount, MidpointRounding.AwayFromZero));

            // PayOS requires description to be max 25 characters, alphanumeric/spaces, and ASCII only.
            string safeDescription = $"Thanh toan DH{orderCode}";
            if (safeDescription.Length > 25)
            {
                safeDescription = safeDescription.Substring(0, 25);
            }

            string signature = GenerateSignature(
                orderCode,
                intAmount,
                safeDescription,
                _options.ReturnUrl,
                _options.CancelUrl,
                _options.ChecksumKey
            );

            long expiredAt = DateTimeOffset.UtcNow
                .Add(Domain.OrderManagement.Aggregate.Order.PaymentWindow)
                .ToUnixTimeSeconds();

            var payload = new
            {
                orderCode,
                amount = intAmount,
                description = safeDescription,
                cancelUrl = _options.CancelUrl,
                returnUrl = _options.ReturnUrl,
                expiredAt,
                signature
            };

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, PaymentRequestsUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            requestMessage.Headers.Add("x-client-id", _options.ClientId);
            requestMessage.Headers.Add("x-api-key", _options.ApiKey);
            requestMessage.Headers.Add("accept", "application/json");

            var response = await _httpClient.SendAsync(requestMessage);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"PayOS Error: {response.StatusCode} - {responseJson}");
            }

            using var document = JsonDocument.Parse(responseJson);
            if (document.RootElement.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Object
                && data.TryGetProperty("checkoutUrl", out var checkoutUrl)
                && checkoutUrl.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(checkoutUrl.GetString()))
            {
                return checkoutUrl.GetString()!;
            }

            throw new Exception("Failed to get checkout URL from PayOS");
        }

        private static string GenerateSignature(
            long orderCode,
            int amount,
            string description,
            string returnUrl,
            string cancelUrl,
            string checksumKey)
        {
            string raw =
                $"amount={amount}&cancelUrl={cancelUrl}&description={description}&orderCode={orderCode}&returnUrl={returnUrl}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
}
