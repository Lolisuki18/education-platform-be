using Application.Features.Orders.Commands.CancelExpiredOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services
{
    /// <summary>
    /// Periodically cancels orders whose payment link expired and returns the coupons they consumed.
    /// </summary>
    public class ExpiredOrderCleanupService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
        private const int MaxBatchesPerRun = 20;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly ILogger<ExpiredOrderCleanupService> _logger;

        public ExpiredOrderCleanupService(
            IServiceScopeFactory scopeFactory,
            Microsoft.Extensions.Configuration.IConfiguration configuration,
            ILogger<ExpiredOrderCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!Microsoft.Extensions.Configuration.ConfigurationBinder.GetValue(_configuration, "Orders:ExpiredOrderCleanupEnabled", true))
                return;

            // Give the database migration at startup a head start
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }
        }

        internal async Task RunOnceAsync(CancellationToken cancellationToken)
        {
            for (var batch = 0; batch < MaxBatchesPerRun; batch++)
            {
                try
                {
                    // One scope (and DbContext) per batch: a failed batch must not poison the next one
                    using var scope = _scopeFactory.CreateScope();
                    var cancelled = await scope.ServiceProvider.GetRequiredService<ISender>()
                        .Send(new CancelExpiredOrdersCommand(), cancellationToken);

                    if (cancelled == 0)
                        return;

                    _logger.LogInformation("Cancelled {Count} expired unpaid orders.", cancelled);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // An order was paid while we were cancelling it: the next batch no longer sees it
                    _logger.LogInformation("An order changed while cancelling expired orders, retrying.");
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to cancel expired orders.");
                    return;
                }
            }
        }
    }
}
