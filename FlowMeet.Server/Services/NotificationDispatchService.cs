namespace FlowMeet.Server.Services;

public class NotificationDispatchService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;

    public NotificationDispatchService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DispatchDueNotificationsAsync(stoppingToken);
            
            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task DispatchDueNotificationsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.DispatchDueScheduledNotificationsAsync(cancellationToken);
    }
}
