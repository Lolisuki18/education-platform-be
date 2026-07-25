using System.Security.Cryptography;
using System.Text;
using Application.Interface;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Infrastructure.Services
{
    public class PayOSPaymentService : IPaymentService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public PayOSPaymentService(IHttpClientFactory factory, IConfiguration config)
        {
            _httpClient = factory.CreateClient("PayOSClient");
            _config = config;
        }

        public async Task<string> CreatePaymentLinkAsync(long orderCode, decimal amount, string description)
        {
            var payos = _config.GetSection("PayOS");
            int intAmount = (int)amount;

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
                payos["ReturnUrl"]!,
                payos["CancelUrl"]!,
                payos["ChecksumKey"]!
            );

            long expiredAt = DateTimeOffset.UtcNow
                .AddMinutes(15)
                .ToUnixTimeSeconds();

            var payload = new
            {
                orderCode,
                amount = intAmount,
                description = safeDescription,
                cancelUrl = payos["CancelUrl"],
                returnUrl = payos["ReturnUrl"],
                expiredAt,
                signature
            };

            var json = JsonConvert.SerializeObject(payload);

            var requestMessage = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api-merchant.payos.vn/v2/payment-requests")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            requestMessage.Headers.Add("x-client-id", payos["ClientId"]);
            requestMessage.Headers.Add("x-api-key", payos["ApiKey"]);
            requestMessage.Headers.Add("accept", "application/json");

            var response = await _httpClient.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode)
            {
                var errorDetails = await response.Content.ReadAsStringAsync();
                throw new Exception($"PayOS Error: {response.StatusCode} - {errorDetails}");
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(responseJson);

            return result?.data?.checkoutUrl ?? throw new Exception("Failed to get checkout URL from PayOS");
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
