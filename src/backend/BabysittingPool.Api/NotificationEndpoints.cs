using BabysittingPool.Application;
using BabysittingPool.Application.Notifications;

namespace BabysittingPool.Api;

internal static class NotificationEndpoints
{
    /// <summary>The flag that hides in-app Notifications (ADR 0016, ADR 0019); removed by #160.</summary>
    private const string Flag = "notifications";

    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/notifications", async (
            HttpContext context, ListNotificationsHandler handler, string? cursor, int? limit, CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await handler.HandleAsync(context.CurrentMember().MemberId, cursor, limit, cancellationToken));
            }
            catch (InvalidCursorException)
            {
                return Problems.InvalidRequest("cursor", "invalid");
            }
        })
        .RequireAuthorization()
        .RequireFeature(Flag)
        .WithName("ListNotifications")
        .Produces<Page<NotificationView>>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        app.MapDelete("/api/notifications/{id:guid}", async (
            Guid id, HttpContext context, DismissNotificationHandler handler, CancellationToken cancellationToken) =>
            await handler.HandleAsync(context.CurrentMember().MemberId, id, cancellationToken)
                ? Results.NoContent()
                : Problems.NotificationNotFound())
        .RequireAuthorization()
        .RequireFeature(Flag)
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
        .RequireFeature(Flag)
        .WithName("DismissAllNotifications")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
