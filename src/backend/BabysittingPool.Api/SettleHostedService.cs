using BabysittingPool.Application.Requests;

namespace BabysittingPool.Api;

/// <summary>
/// Runs Settle about every minute in the one API process (ADR 0007). Off in tests and E2E, which move the
/// clock and call Settle directly.
/// </summary>
internal sealed partial class SettleHostedService(IServiceScopeFactory scopes, ILogger<SettleHostedService> logger) : BackgroundService
{
    public const string EnabledSetting = "Settle:Enabled";

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<SettleHandler>().HandleAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed tick is retried on the next one; it must never stop the loop.
                LogTickFailed(ex);
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "SettleTickFailed")]
    private partial void LogTickFailed(Exception exception);
}
