using BabysittingPool.Application;
using BabysittingPool.Application.Addresses;
using BabysittingPool.Application.Requests;
using BabysittingPool.Domain.Credits;
using Microsoft.AspNetCore.Diagnostics;

namespace BabysittingPool.Api;

/// <summary>
/// The one place a failure raised below the API becomes a Problem (ADR 0006), so an endpoint is a route plus a
/// handler call and a new handler needs no catch blocks. An exception it does not know is not handled here.
/// </summary>
internal sealed class DomainFailureHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            DomainException domain => Problems.DomainRule(domain),
            ConcurrencyConflictException => Problems.Conflict(),
            InvalidCursorException => Problems.InvalidRequest("cursor", "invalid"),
            AddressQueryTooShortException => Problems.InvalidRequest("q", "too_short"),
            AddressServiceUnavailableException => Problems.AddressServiceUnavailable(),
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        await problem.ExecuteAsync(httpContext);
        return true;
    }
}
