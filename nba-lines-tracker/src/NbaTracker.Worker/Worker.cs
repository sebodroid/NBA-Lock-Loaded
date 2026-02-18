namespace NbaTracker.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started at {time}", DateTimeOffset.UtcNow);
        // Phase 2 will implement actual sync logic
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
