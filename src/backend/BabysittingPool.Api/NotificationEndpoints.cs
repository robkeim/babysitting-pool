using System.Text.Json;
using BabysittingPool.Application;
using BabysittingPool.Application.Notifications;
using Microsoft.Extensions.Options;

namespace BabysittingPool.Api;

internal static class NotificationEndpoints
{
    public const string StreamPath = "/api/notifications/stream";

    /// <summary>A comment line that keeps proxies from closing an idle stream (ADR 0019).</summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        // Not in OpenAPI: it cannot model an event stream, so the contract is in ADR 0019 and the spec.
        app.MapGet(StreamPath, StreamAsync)
            .RequireAuthorization()
            .ExcludeFromDescription();

        app.MapGet("/api/notifications", async (
            HttpContext context, ListNotificationsHandler handler, string? cursor, int? limit, CancellationToken cancellationToken) =>
        {
            return Results.Ok(await handler.HandleAsync(context.CurrentMember().MemberId, cursor, limit, cancellationToken));
        })
        .RequireAuthorization()
        .WithName("ListNotifications")
        .Produces<Page<NotificationView>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapDelete("/api/notifications/{id:guid}", async (
            Guid id, HttpContext context, DismissNotificationHandler handler, CancellationToken cancellationToken) =>
            await handler.HandleAsync(context.CurrentMember().MemberId, id, cancellationToken)
                ? Results.NoContent()
                : Problems.NotificationNotFound())
        .RequireAuthorization()
        .WithName("DismissNotification")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapDelete("/api/notifications", async (
            HttpContext context, DismissAllNotificationsHandler handler, CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(context.CurrentMember().MemberId, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("DismissAllNotifications")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// Holds the response open and writes each event for the signed-in Member as it is published, plus a heartbeat
    /// comment while idle. There is no replay: a client that reconnects refetches the list (ADR 0019).
    /// </summary>
    private static async Task StreamAsync(
        HttpContext context,
        INotificationSubscriptions subscriptions,
        IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions> json,
        TimeProvider clock)
    {
        var cancellationToken = context.RequestAborted;
        var response = context.Response;
        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        // Tells nginx-style proxies not to buffer the stream.
        response.Headers["X-Accel-Buffering"] = "no";
        context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();

        // Subscribe before the first byte, so everything published after the client sees the headers reaches it.
        using var subscription = subscriptions.Subscribe(context.CurrentMember().MemberId);
        using var heartbeat = new PeriodicTimer(HeartbeatInterval, clock);
        try
        {
            await response.WriteAsync(": connected\n\n", cancellationToken);
            await response.Body.FlushAsync(cancellationToken);

            var reader = subscription.Events;
            var next = reader.WaitToReadAsync(cancellationToken).AsTask();
            var tick = heartbeat.WaitForNextTickAsync(cancellationToken).AsTask();
            while (true)
            {
                var finished = await Task.WhenAny(next, tick);
                if (finished == tick)
                {
                    if (!await tick)
                    {
                        return;
                    }

                    await response.WriteAsync(": heartbeat\n\n", cancellationToken);
                    tick = heartbeat.WaitForNextTickAsync(cancellationToken).AsTask();
                }
                else
                {
                    // False once the feed is closed, e.g. this connection fell too far behind: the client reconnects.
                    if (!await next)
                    {
                        return;
                    }

                    while (reader.TryRead(out var notificationEvent))
                    {
                        await response.WriteAsync(Format(notificationEvent, json.Value.SerializerOptions), cancellationToken);
                    }

                    next = reader.WaitToReadAsync(cancellationToken).AsTask();
                }

                await response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // The client went away; disposing the subscription cleans up.
        }
    }

    private static string Format(NotificationEvent notificationEvent, JsonSerializerOptions options)
    {
        object? data = notificationEvent switch
        {
            NotificationCreated created => created.Notification,
            NotificationDismissed dismissed => new { id = dismissed.Id },
            _ => new { },
        };
        return $"event: {notificationEvent.Name}\ndata: {JsonSerializer.Serialize(data, options)}\n\n";
    }
}
