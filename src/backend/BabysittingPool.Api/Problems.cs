using BabysittingPool.Domain.Credits;

namespace BabysittingPool.Api;

/// <summary>Non-2xx responses: <c>application/problem+json</c> with a stable machine-readable <c>code</c> (ADR 0006).</summary>
internal static class Problems
{
    public static IResult Unauthenticated() =>
        Problem(StatusCodes.Status401Unauthorized, "unauthenticated", "Not signed in.");

    public static IResult NotAdmin() =>
        Problem(StatusCodes.Status403Forbidden, "not_admin", "Only the Admin can do this.");

    public static IResult CsrfHeaderMissing() =>
        Problem(StatusCodes.Status403Forbidden, "csrf_header_missing", "State-changing calls need the X-Requested-With header.");

    public static IResult InvitationInvalid() =>
        Problem(StatusCodes.Status422UnprocessableEntity, "invitation_invalid", "The Invitation is unknown, used or expired.");

    public static IResult SignInLinkInvalid() =>
        Problem(StatusCodes.Status422UnprocessableEntity, "sign_in_link_invalid", "The sign-in link is unknown, used or expired.");

    public static IResult TooManyRequests() =>
        Problem(StatusCodes.Status429TooManyRequests, "rate_limited", "Too many requests; try again later.");

    public static IResult InvalidRequest(string field, string fieldCode) =>
        Problem(
            StatusCodes.Status400BadRequest,
            "invalid_request",
            "The request is malformed.",
            new Dictionary<string, string[]> { [field] = [fieldCode] });

    /// <summary>
    /// A business rule said no (spec §4); the code is the one the domain raised. A Request that is no longer in
    /// the right state is a conflict (409), anything else a rule violation (422), per ADR 0006.
    /// </summary>
    public static IResult DomainRule(DomainException exception) =>
        Problem(
            exception.Code == "invalid_state" ? StatusCodes.Status409Conflict : StatusCodes.Status422UnprocessableEntity,
            exception.Code,
            exception.Message);

    public static IResult Conflict() =>
        Problem(StatusCodes.Status409Conflict, "concurrent_update", "The state changed meanwhile; try again.");

    public static IResult AccountNotFound() =>
        Problem(StatusCodes.Status404NotFound, "account_not_found", "The Member has no Credit Account.");

    public static IResult RequestNotFound() =>
        Problem(StatusCodes.Status404NotFound, "request_not_found", "There is no such Request.");

    private static IResult Problem(
        int status, string code, string title, Dictionary<string, string[]>? errors = null)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = code };
        if (errors is not null)
        {
            extensions["errors"] = errors;
        }

        return Results.Problem(statusCode: status, title: title, extensions: extensions);
    }
}
