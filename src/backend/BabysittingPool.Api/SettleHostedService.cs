using BabysittingPool.Application.Requests;
using Serilog.Context;

namespace BabysittingPool.Api;

/// <summary>
/// Runs Settle about every minute in the one API process (ADR 0007). Off in tests and E2E, which move the
/// clock and call Settle directly.
/// </summary>
internal sealed partial class SettleHostedService(
    IServiceScopeFactory scopes, TimeProvider clock, ILogger<SettleHostedService> logger) : BackgroundService
{
    public const string EnabledSetting = "Settle:Enabled";

    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, clock);
        do
        {
            // Each tick is its own trace (ADR 0010), failure included.
            using (LogContext.PushProperty("TraceId", Correlation.NewTraceId()))
            using (LogContext.PushProperty("Source", Correlation.SettleSource))
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
