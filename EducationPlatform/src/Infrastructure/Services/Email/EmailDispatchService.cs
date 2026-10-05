using Application.Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Email
{
    /// <summary>Sends queued e-mails one at a time, retrying with a growing delay before giving up.</summary>
    public class EmailDispatchService : BackgroundService
    {
        private static readonly TimeSpan[] RetryDelays =
        {
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30)
        };

        private readonly EmailQueue _queue;
        private readonly IEmailSender _sender;
        private readonly ILogger<EmailDispatchService> _logger;

        public EmailDispatchService(EmailQueue queue, IEmailSender sender, ILogger<EmailDispatchService> logger)
        {
            _queue = queue;
            _sender = sender;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Reads until the queue is completed (see StopAsync), so what is already queued still goes out on shutdown
            await foreach (var email in _queue.Reader.ReadAllAsync(CancellationToken.None))
            {
                await SendWithRetryAsync(email, stoppingToken);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _queue.Complete();
            await base.StopAsync(cancellationToken);
        }

        internal async Task SendWithRetryAsync(EmailMessage email, CancellationToken stoppingToken)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await _sender.SendAsync(email, CancellationToken.None);
                    PlatformMetrics.EmailsSent.Add(1, new KeyValuePair<string, object?>("result", "sent"));
                    return;
                }
                catch (Exception ex)
                {
                    var willRetry = attempt < RetryDelays.Length && !stoppingToken.IsCancellationRequested;
                    if (!willRetry)
                    {
                        PlatformMetrics.EmailsSent.Add(1, new KeyValuePair<string, object?>("result", "failed"));
                        _logger.LogError(ex, "Giving up sending an e-mail to {To} after {Attempts} attempt(s).", LogMask.Email(email.To), attempt + 1);
                        return;
                    }

                    _logger.LogWarning(ex, "Sending an e-mail to {To} failed (attempt {Attempt}), retrying.", LogMask.Email(email.To), attempt + 1);

                    try
                    {
                        await Task.Delay(RetryDelays[attempt], stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Shutting down: one last attempt on the next loop iteration, no more waiting
                    }
                }
            }
        }
    }
}
