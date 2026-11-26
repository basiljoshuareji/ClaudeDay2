using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TodoApp.Api.Data;

namespace TodoApp.Api.Infrastructure;

/// <summary>Permanently removes soft-deleted tasks once the undo window has passed.</summary>
public class TrashPurgeService(
    IServiceScopeFactory scopeFactory,
    IOptions<TrashOptions> options,
    TimeProvider clock,
    ILogger<TrashPurgeService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.PurgeEnabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, settings.IntervalMinutes)));
        try
        {
            do
            {
                await PurgeOnceAsync(settings, stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }

    private async Task PurgeOnceAsync(TrashOptions settings, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cutoff = clock.GetUtcNow().UtcDateTime.AddHours(-settings.RetentionHours);

            var purged = await db.Tasks.IgnoreQueryFilters()
                .Where(t => t.DeletedAt != null && t.DeletedAt < cutoff)
                .ExecuteDeleteAsync(ct);

            if (purged > 0)
            {
                logger.LogInformation("Purged {Count} soft-deleted tasks older than {Hours}h", purged, settings.RetentionHours);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to purge soft-deleted tasks");
        }
    }
}
