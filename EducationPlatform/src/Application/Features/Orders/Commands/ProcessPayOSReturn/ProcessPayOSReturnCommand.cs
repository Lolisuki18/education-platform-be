using Application.Features.Orders.Commands.FinishOrder;
using Application.Interface;
using Application.Options;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Application.Features.Orders.Commands.ProcessPayOSReturn
{
    public class PayOSReturnResult
    {
        /// <summary>False when the redirect carried no valid PayOS signature and must be rejected.</summary>
        public bool IsSignatureValid { get; set; }

        public bool IsSuccess { get; set; }

        public long OrderCode { get; set; }
    }

    /// <summary>The query-string PayOS appends when it redirects the browser back after payment.</summary>
    public class ProcessPayOSReturnCommand : IRequest<PayOSReturnResult>
    {
        public string? Status { get; set; }
        public string? OrderCode { get; set; }
        public string? Id { get; set; }
        public string? Code { get; set; }
        public string? Signature { get; set; }
        public string? Amount { get; set; }
        public string? Cancel { get; set; }
    }

    public class ProcessPayOSReturnCommandHandler : IRequestHandler<ProcessPayOSReturnCommand, PayOSReturnResult>
    {
        private readonly IPayOSSignatureVerifier _signatureVerifier;
        private readonly ISender _sender;
        private readonly PayOSOptions _options;
        private readonly ILogger<ProcessPayOSReturnCommandHandler> _logger;

        public ProcessPayOSReturnCommandHandler(
            IPayOSSignatureVerifier signatureVerifier,
            ISender sender,
            IOptions<PayOSOptions> options,
            ILogger<ProcessPayOSReturnCommandHandler> logger)
        {
            _signatureVerifier = signatureVerifier;
            _sender = sender;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<PayOSReturnResult> Handle(ProcessPayOSReturnCommand request, CancellationToken cancellationToken)
        {
            long.TryParse(request.OrderCode, out var orderCode);

            var result = new PayOSReturnResult { OrderCode = orderCode };

            if (string.IsNullOrEmpty(request.Signature) ||
                !_signatureVerifier.VerifyRedirectSignature(
                    request.Amount ?? "",
                    request.Cancel ?? "",
                    request.Code ?? "",
                    request.Id ?? "",
                    request.OrderCode ?? "",
                    request.Status ?? "",
                    request.Signature,
                    _options.ChecksumKey))
            {
                return result;
            }

            result.IsSignatureValid = true;

            var cancelled = string.Equals(request.Cancel, "true", StringComparison.OrdinalIgnoreCase);
            result.IsSuccess = request.Status == "PAID" && !cancelled;

            if (!result.IsSuccess)
                return result;

            try
            {
                // Finish right away so the student sees the course as soon as the browser lands on the frontend.
                await _sender.Send(new FinishOrderCommand { OrderCode = orderCode }, cancellationToken);
            }
            catch (Exception ex)
            {
                // The signed PayOS webhook is the authoritative path and will retry, so the redirect still succeeds.
                _logger.LogError(ex, "Could not finish order {OrderCode} from the payment redirect.", orderCode);
            }

            return result;
        }
    }
}
