using Quartz;

namespace TennisBruck.Services;

public class ItnSyncJob(IServiceProvider serviceProvider, ILogger<ItnSyncJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            await SyncItnScoresAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while syncing ITN scores.");
        }
    }

    private async Task SyncItnScoresAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting ITN sync process via Quartz...");

        // Create a new scope to resolve scoped services like DbContext
        using var scope = serviceProvider.CreateScope();
        
        var dbContext = scope.ServiceProvider.GetRequiredService<TennisDb.TennisContext>();
        var scraperService = scope.ServiceProvider.GetRequiredService<OetvScraperService>();

        // Find ALL players so we can map those without a URL
        var allPlayers = await dbContext.Players.ToListAsync(stoppingToken);

        int updatedCount = 0;

        foreach (var player in allPlayers)
        {
            if (stoppingToken.IsCancellationRequested) break;

            // 1. Attempt to Auto-Map if they have no URL
            if (string.IsNullOrEmpty(player.NuLigaPlayerUrl) && 
                !string.IsNullOrEmpty(player.Firstname) && 
                !string.IsNullOrEmpty(player.Lastname))
            {
                logger.LogInformation("Attempting to automatically map player {First} {Last}...", player.Firstname, player.Lastname);
                
                player.NuLigaPlayerUrl = await scraperService.AutomaticallyFindPlayerUrlAsync(
                    player.Firstname, 
                    player.Lastname, 
                    "ASKÖ Bruck - Peuerbach");

                // If mapping succeeded, save now so we don't lose it if the process crashes later
                if (!string.IsNullOrEmpty(player.NuLigaPlayerUrl))
                {
                    logger.LogInformation("Successfully mapped profile for {First} {Last}.", player.Firstname, player.Lastname);
                    updatedCount++; // Count mapping as an update
                    await dbContext.SaveChangesAsync(stoppingToken); 
                }
                
                // Polite delay after search
                await Task.Delay(Random.Shared.Next(1500, 3000), stoppingToken);
            }

            // 2. Fetch ITN if they have a URL
            if (!string.IsNullOrEmpty(player.NuLigaPlayerUrl))
            {
                var newItn = await scraperService.GetPlayerItnAsync(player.NuLigaPlayerUrl);
                
                if (newItn.HasValue && newItn.Value != player.Itn)
                {
                    player.Itn = newItn;
                    player.LastItnUpdate = DateTime.UtcNow;
                    updatedCount++;
                }

                // Be polite to the OETV servers, wait a little bit between requests
                await Task.Delay(Random.Shared.Next(1000, 3000), stoppingToken);
            }
        }

        if (updatedCount > 0)
        {
            await dbContext.SaveChangesAsync(stoppingToken);
            logger.LogInformation("Successfully updated ITN for {Count} players.", updatedCount);
        }
        else
        {
            logger.LogInformation("No ITN updates were necessary.");
        }
    }
}
