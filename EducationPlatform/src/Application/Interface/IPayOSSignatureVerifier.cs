namespace Application.Interface
{
    public interface IPayOSSignatureVerifier
    {
        bool VerifyWebhookSignature(IDictionary<string, string?> data, string signature, string checksumKey);

        bool VerifyRedirectSignature(
            string amount,
            string cancel,
            string code,
            string id,
            string orderCode,
            string status,
            string signature,
            string checksumKey);
    }
}
