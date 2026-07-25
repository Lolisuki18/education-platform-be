using System.Security.Cryptography;
using System.Text;
using Application.Interface;

namespace Infrastructure.Services
{
    public class PayOSSignatureVerifier : IPayOSSignatureVerifier
    {
        public bool VerifyWebhookSignature(IDictionary<string, string?> data, string signature, string checksumKey)
        {
            if (string.IsNullOrEmpty(signature))
                return false;

            var queryString = string.Join("&", data
                .Where(p => p.Key != "signature")
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{p.Key}={p.Value ?? string.Empty}"));

            return string.Equals(Sign(queryString, checksumKey), signature, StringComparison.OrdinalIgnoreCase);
        }

        public bool VerifyRedirectSignature(
            string amount,
            string cancel,
            string code,
            string id,
            string orderCode,
            string status,
            string signature,
            string checksumKey)
        {
            if (string.IsNullOrEmpty(signature))
                return false;

            // amount is not returned in PayOS redirect callbacks.
            string raw = $"cancel={cancel.ToLowerInvariant()}&code={code}&id={id}&orderCode={orderCode}&status={status}";

            return string.Equals(Sign(raw, checksumKey), signature, StringComparison.OrdinalIgnoreCase);
        }

        private static string Sign(string raw, string checksumKey)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(checksumKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
