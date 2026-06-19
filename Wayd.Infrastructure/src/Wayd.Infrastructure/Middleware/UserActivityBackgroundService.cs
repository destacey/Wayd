using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Wayd.Infrastructure.Middleware;

public class UserActivityBackgroundService(
    IServiceProvider serviceProvider,
    ILogger<UserActivityBackgroundService> logger) : BackgroundService
{
    private readonly Channel<string> _channel = Channel.CreateBounded<string>(
        new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<UserActivityBackgroundService> _logger = logger;

    public void QueueUpdate(string userId)
    {
        _channel.Writer.TryWrite(userId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var userId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<WaydDbContext>();
                var dateTimeProvider = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

                var user = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.Id == userId, stoppingToken);

                if (user != null)
                {
                    user.LastActivityAt = dateTimeProvider.Now;
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update last activity for user {UserId}", userId);
            }
        }
    }
}
