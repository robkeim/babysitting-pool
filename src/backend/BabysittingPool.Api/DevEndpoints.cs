using BabysittingPool.Infrastructure;

namespace BabysittingPool.Api;

/// <summary>
/// Development and E2E only: reads the fake outbox, where sign-in and Invitation links land (ADR 0011).
/// Not part of the contract, so kept out of the OpenAPI document.
/// </summary>
internal static class DevEndpoints
{
    public static void MapDevEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dev/outbox", (EmailOutbox outbox, string? to) =>
            outbox.Messages
                .Where(m => to is null || string.Equals(m.To, to, StringComparison.OrdinalIgnoreCase))
                .Select(m => new OutboxMessage(m.To, m.Subject, m.Link.ToString())))
            .AllowAnonymous()
            .ExcludeFromDescription();
    }

    private sealed record OutboxMessage(string To, string Subject, string Link);
}
