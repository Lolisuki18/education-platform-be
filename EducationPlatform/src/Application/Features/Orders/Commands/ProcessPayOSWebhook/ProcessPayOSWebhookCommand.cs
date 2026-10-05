using System.Text.Json;
using Application.Common;
using Application.Exceptions;
using Application.Features.Orders.Commands.FinishOrder;
using Application.Interface;
using Application.Options;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Features.Orders.Commands.ProcessPayOSWebhook
{
    public enum PayOSWebhookResult
    {
        /// <summary>The payment was applied to its order (or the order was already paid).</summary>
        Processed,

        /// <summary>Valid request that needs no action: empty ping, failed payment, unknown order, ...</summary>
        Ignored,

        InvalidSignature,

        Malformed
    }

    public class ProcessPayOSWebhookCommand : IRequest<PayOSWebhookResult>
    {
        public string Body { get; set; } = string.Empty;
    }

    public class ProcessPayOSWebhookCommandHandler : IRequestHandler<ProcessPayOSWebhookCommand, PayOSWebhookResult>
    {
        private readonly IPayOSSignatureVerifier _signatureVerifier;
        private readonly ISender _sender;
        private readonly PayOSOptions _options;
        private readonly ILogger<ProcessPayOSWebhookCommandHandler> _logger;

        public ProcessPayOSWebhookCommandHandler(
            IPayOSSignatureVerifier signatureVerifier,
            ISender sender,
            IOptions<PayOSOptions> options,
            ILogger<ProcessPayOSWebhookCommandHandler> logger)
        {
            _signatureVerifier = signatureVerifier;
            _sender = sender;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<PayOSWebhookResult> Handle(ProcessPayOSWebhookCommand request, CancellationToken cancellationToken)
        {
            var result = await ProcessAsync(request, cancellationToken);

            // A jump in InvalidSignature or Malformed is somebody probing the endpoint; none of Processed means PayOS cannot reach us
            PlatformMetrics.PaymentWebhooks.Add(1, new KeyValuePair<string, object?>("result", result.ToString()));
            return result;
        }

        private async Task<PayOSWebhookResult> ProcessAsync(ProcessPayOSWebhookCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Body))
                return PayOSWebhookResult.Ignored;

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(request.Body);
            }
            catch (JsonException)
            {
                return PayOSWebhookResult.Malformed;
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("signature", out var signatureElement)
                    || signatureElement.ValueKind != JsonValueKind.String
                    || !root.TryGetProperty("data", out var data)
                    || data.ValueKind != JsonValueKind.Object)
                {
                    return PayOSWebhookResult.Malformed;
                }

                var fields = data.EnumerateObject()
                    .ToDictionary(p => p.Name, p => (string?)ToSignedString(p.Value), StringComparer.Ordinal);

                if (!_signatureVerifier.VerifyWebhookSignature(fields, signatureElement.GetString()!, _options.ChecksumKey))
                    return PayOSWebhookResult.InvalidSignature;

                var code = root.TryGetProperty("code", out var codeElement) ? codeElement.ToString() : null;
                if (code != "00")
                    return PayOSWebhookResult.Ignored;

                if (!data.TryGetProperty("orderCode", out var orderCodeElement)
                    || !orderCodeElement.TryGetInt64(out var orderCode))
                {
                    return PayOSWebhookResult.Malformed;
                }

                long? amount = data.TryGetProperty("amount", out var amountElement) && amountElement.TryGetInt64(out var parsedAmount)
                    ? parsedAmount
                    : null;

                try
                {
                    // Errors other than the two below propagate on purpose: a 5xx makes PayOS retry the webhook.
                    await _sender.Send(new FinishOrderCommand { OrderCode = orderCode, PaidAmount = amount }, cancellationToken);
                    return PayOSWebhookResult.Processed;
                }
                catch (NotFoundException)
                {
                    // PayOS also fires test webhooks for orders that do not exist here
                    _logger.LogWarning("PayOS webhook received for unknown order {OrderCode}.", orderCode);
                    return PayOSWebhookResult.Ignored;
                }
                catch (BadRequestException ex)
                {
                    _logger.LogError("PayOS webhook rejected for order {OrderCode}: {Reason}", orderCode, ex.Message);
                    return PayOSWebhookResult.Ignored;
                }
            }
        }

        /// <summary>Renders a value the way PayOS did when it signed the payload.</summary>
        private static string ToSignedString(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.String => value.GetString() ?? string.Empty,
                _ => value.GetRawText()
            };
        }
    }
}
