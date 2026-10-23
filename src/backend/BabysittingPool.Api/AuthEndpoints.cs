using BabysittingPool.Application.Identity;
using BabysittingPool.Domain.Identity;

namespace BabysittingPool.Api;

internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/sign-in-links", async (
            SignInLinkRequest request, RequestSignInLinkHandler handler, CancellationToken cancellationToken) =>
        {
            if (!EmailAddress.TryParse(request.Email, out var email))
            {
                return Problems.InvalidRequest("email", "invalid");
            }

            await handler.HandleAsync(email, cancellationToken);
            return Results.Accepted();
        })
        .AllowAnonymous()
        .WithName("RequestSignInLink")
        .Produces(StatusCodes.Status202Accepted)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        app.MapPost("/api/auth/sessions", async (
            HttpContext context, TokenRequest request, RedeemSignInLinkHandler handler, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token) || await handler.HandleAsync(request.Token, cancellationToken) is not { } signedIn)
            {
                return Problems.SignInLinkInvalid();
            }

            SessionCookie.Set(context, signedIn.SessionSecret);
            return Results.NoContent();
        })
        .AllowAnonymous()
        .WithName("StartSession")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        app.MapDelete("/api/auth/sessions/current", async (
            HttpContext context, EndSessionHandler handler, CancellationToken cancellationToken) =>
        {
            if (SessionCookie.Read(context) is { Length: > 0 } secret)
            {
                await handler.HandleAsync(secret, cancellationToken);
            }

            SessionCookie.Clear(context);
            return Results.NoContent();
        })
        .AllowAnonymous()
        .WithName("EndSession")
        .Produces(StatusCodes.Status204NoContent);

        app.MapPost("/api/invitations/preview", async (
            TokenRequest request, PreviewInvitationHandler handler, CancellationToken cancellationToken) =>
        {
            var email = string.IsNullOrWhiteSpace(request.Token) ? null : await handler.HandleAsync(request.Token, cancellationToken);
            return email is null ? Problems.InvitationInvalid() : Results.Ok(new InvitationPreview(email));
        })
        .AllowAnonymous()
        .WithName("PreviewInvitation")
        .Produces<InvitationPreview>()
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/api/invitations/accept", async (
            HttpContext context, AcceptInvitationRequest request, AcceptInvitationHandler handler, CancellationToken cancellationToken) =>
        {
            if (!DisplayName.TryCreate(request.DisplayName, out var displayName))
            {
                return Problems.InvalidRequest("displayName", "invalid");
            }

            if (string.IsNullOrWhiteSpace(request.Token)
                || await handler.HandleAsync(new AcceptInvitationCommand(request.Token, displayName), cancellationToken) is not { } signedIn)
            {
                return Problems.InvitationInvalid();
            }

            SessionCookie.Set(context, signedIn.SessionSecret);
            return Results.NoContent();
        })
        .AllowAnonymous()
        .WithName("AcceptInvitation")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/api/me", (HttpContext context) => Results.Ok(context.CurrentMember()))
        .RequireAuthorization()
        .WithName("GetMe")
        .Produces<MemberView>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        app.MapPut("/api/me/language", async (
            HttpContext context, LanguageRequest request, ChangeLanguageHandler handler, IHostEnvironment environment,
            CancellationToken cancellationToken) =>
        {
            var member = context.CurrentMember();

            // Ünïcödé Tëst is a test aid, never offered in production (GLOSSARY: Ünïcödé Tëst).
            if (!Language.TryParse(request.Language, includeTest: !environment.IsProduction(), out var language))
            {
                return Problems.InvalidRequest("language", "unsupported");
            }

            await handler.HandleAsync(member.MemberId, language, cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("ChangeMyLanguage")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private sealed record LanguageRequest(string? Language);

    private sealed record SignInLinkRequest(string? Email);

    private sealed record TokenRequest(string? Token);

    private sealed record AcceptInvitationRequest(string? Token, string? DisplayName);

    private sealed record InvitationPreview(string Email);
}
