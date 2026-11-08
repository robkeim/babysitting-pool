using System.Reflection;
using BabysittingPool.Application.Identity;
using BabysittingPool.Domain.Identity;

namespace BabysittingPool.Tests.Support;

/// <summary>Runs a callback once the wrapped store has answered the Member lookup.</summary>
public class AcceptingAfterMemberLookup : DispatchProxy
{
    private IIdentityStore _inner = null!;
    private Func<Task> _afterLookup = null!;

    public static IIdentityStore Wrap(IIdentityStore inner, Func<Task> afterLookup)
    {
        var proxy = Create<IIdentityStore, AcceptingAfterMemberLookup>();
        var self = (AcceptingAfterMemberLookup)(object)proxy;
        self._inner = inner;
        self._afterLookup = afterLookup;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        var result = targetMethod!.Invoke(_inner, args);
        return targetMethod.Name == nameof(IIdentityStore.FindActiveMemberAsync)
            ? AfterAsync((Task<Member?>)result!)
            : result;
    }

    private async Task<Member?> AfterAsync(Task<Member?> lookup)
    {
        var member = await lookup;
        await _afterLookup();
        return member;
    }
}
