using Microsoft.EntityFrameworkCore;
using TennisDb;

namespace TennisBruck.Services;

public class ItnSyncBackgroundService(IServiceProvider serviceProvider, ILogger<ItnSyncBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ITN Sync Background Service is starting.");

        // Wait a few seconds before the first run so the application can fully start
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncItnScoresAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while syncing ITN scores.");
            }

            // Sleep for 24 hours (or whatever interval is desired)
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task SyncItnScoresAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting ITN sync process...");

        // Create a new scope to resolve scoped services like DbContext
        using var scope = serviceProvider.CreateScope();
        
        var dbContext = scope.ServiceProvider.GetRequiredService<TennisContext>();
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
