using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TestGrid.Changes;

public class BackgroundDataChanger(
    DataChangeGenerator generator,
    DataChangeOptions options,
    ILogger<BackgroundDataChanger> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ApplyChangesSafelyAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ApplyChangesSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await generator.ApplyChangesAsync(stoppingToken);
            logger.LogDebug("Applied {Count} inserts, updates and deletes", options.ChangesPerType);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Background data change failed");
        }
    }
}
