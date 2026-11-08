using AwesomeAssertions;
using BabysittingPool.Api;
using BabysittingPool.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BabysittingPool.Tests.Api;

public class SettleHostedServiceTests : IAsyncLifetime
{
    private TestDatabase _database = null!;

    public async Task InitializeAsync() => _database = await TestDatabase.CreateMigratedAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task A_failed_tick_is_logged_and_the_next_tick_still_runs_and_shutdown_ends_the_loop_cleanly()
    {
        var scopes = new FailsFirstScopeFactory(_database.Services.GetRequiredService<IServiceScopeFactory>());
        var logger = new CapturingLogger();
        using var service = new SettleHostedService(scopes, _database.Clock, logger);

        await service.StartAsync(default);
        await WaitForAsync(() => scopes.Created == 1);
        _database.Clock.Advance(SettleHostedService.Interval);
        await WaitForAsync(() => scopes.Created == 2);

        logger.Errors.Should().ContainSingle().Which.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("tick failed");
        var stop = () => service.StopAsync(default);
        await stop.Should().NotThrowAsync();
        service.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue();
        _database.Clock.Advance(SettleHostedService.Interval);
        scopes.Created.Should().Be(2);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class FailsFirstScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        private int _created;

        public int Created => Volatile.Read(ref _created);

        public IServiceScope CreateScope()
        {
            if (Interlocked.Increment(ref _created) == 1)
            {
                throw new InvalidOperationException("tick failed");
            }

            return inner.CreateScope();
        }
    }

    private sealed class CapturingLogger : ILogger<SettleHostedService>
    {
        public List<Exception> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error && exception is not null)
            {
                Errors.Add(exception);
            }
        }
    }
}
