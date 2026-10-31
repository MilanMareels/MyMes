using MyMES.Application.Integrations;

namespace MyMES.Api.BackgroundServices;

public sealed class ErpIngestionWorker(
    IServiceProvider services,
    ILogger<ErpIngestionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = services.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IErpIngestionService>();
                var processed = await svc.ProcessPendingAsync(stoppingToken);

                if (processed > 0)
                {
                    logger.LogInformation("ERP worker: {Count} message(s) processed.", processed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ERP worker failed.");
            }
        }
    }
}
