using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;

namespace BabysittingPool.Tests.Support;

/// <summary>
/// Runs a test's hook just before a store method reaches the real store, to lose a race for real (change the row the
/// handler read) or to fail. Everything else goes through to the real SQLite store. Registered once per test by
/// <see cref="Intercept{T}"/>; each hook runs once, so a retry on the next run meets the real store.
/// </summary>
internal sealed class StoreFaults
{
    private readonly Dictionary<string, (Func<object?[], bool> When, Func<object?[], Task> Hook)> _hooks = [];

    /// <summary>
    /// Runs <paramref name="hook"/> once before the named method, for the first call <paramref name="when"/> accepts.
    /// A hook that throws fails the call.
    /// </summary>
    public void Before(string method, Func<object?[], Task> hook, Func<object?[], bool>? when = null) =>
        _hooks[method] = (when ?? (_ => true), hook);

    public async Task RunAsync(string method, object?[] args)
    {
        if (_hooks.TryGetValue(method, out var entry) && entry.When(args))
        {
            _hooks.Remove(method);
            await entry.Hook(args);
        }
    }

    /// <summary>Wraps the registered <typeparamref name="T"/> so its calls pass through <see cref="StoreFaults"/> first.</summary>
    public static void Intercept<T>(IServiceCollection services) where T : class
    {
        var inner = services.Last(d => d.ServiceType == typeof(T));
        services.Remove(inner);
        services.AddSingleton<StoreFaults>();
        services.AddScoped(sp =>
        {
            var target = (T)(inner.ImplementationFactory?.Invoke(sp)
                ?? ActivatorUtilities.CreateInstance(sp, inner.ImplementationType!));
            var proxy = DispatchProxy.Create<T, FaultProxy<T>>();
            ((FaultProxy<T>)(object)proxy).Target = target;
            ((FaultProxy<T>)(object)proxy).Faults = sp.GetRequiredService<StoreFaults>();
            return proxy;
        });
    }
}

public class FaultProxy<T> : DispatchProxy where T : class
{
    private static readonly MethodInfo DelayedGeneric =
        typeof(FaultProxy<T>).GetMethod(nameof(DelayedResultAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    internal T Target { get; set; } = null!;

    internal StoreFaults Faults { get; set; } = null!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var method = targetMethod!;
        var arguments = args ?? [];
        if (method.ReturnType == typeof(Task))
        {
            return DelayedAsync(method, arguments);
        }

        return DelayedGeneric.MakeGenericMethod(method.ReturnType.GetGenericArguments()[0]).Invoke(this, [method, arguments]);
    }

    private async Task DelayedAsync(MethodInfo method, object?[] args)
    {
        await Faults.RunAsync(method.Name, args);
        await (Task)InvokeTarget(method, args);
    }

    private async Task<TResult> DelayedResultAsync<TResult>(MethodInfo method, object?[] args)
    {
        await Faults.RunAsync(method.Name, args);
        return await (Task<TResult>)InvokeTarget(method, args);
    }

    private object InvokeTarget(MethodInfo method, object?[] args)
    {
        try
        {
            return method.Invoke(Target, args)!;
        }
        catch (TargetInvocationException ex)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException!).Throw();
            throw;
        }
    }
}
