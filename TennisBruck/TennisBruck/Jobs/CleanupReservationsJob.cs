using Quartz;

namespace TennisBruck.Jobs;

public class CleanupReservationsJob(IServiceProvider serviceProvider, ILogger<CleanupReservationsJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            logger.LogInformation("Starting cleanup of old reservations...");

            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TennisDb.TennisContext>();

            var oldSlots = await db.Reservations
                .Where(s => s.StartTime.Date < DateTime.Today)
                .ToListAsync(context.CancellationToken);

            if (oldSlots.Any())
            {
                db.Reservations.RemoveRange(oldSlots);
                await db.SaveChangesAsync(context.CancellationToken);
                logger.LogInformation("Successfully deleted {Count} old reservations.", oldSlots.Count);
            }
            else
            {
                logger.LogInformation("No old reservations found to delete.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while cleaning up old reservations.");
        }
    }
}
