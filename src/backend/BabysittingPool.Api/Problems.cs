namespace BabysittingPool.Api;

/// <summary>Non-2xx responses: <c>application/problem+json</c> with a stable machine-readable <c>code</c> (ADR 0006).</summary>
internal static class Problems
{
    public static IResult Unauthenticated() =>
        Problem(StatusCodes.Status401Unauthorized, "unauthenticated", "Not signed in.");

    public static IResult CsrfHeaderMissing() =>
        Problem(StatusCodes.Status403Forbidden, "csrf_header_missing", "State-changing calls need the X-Requested-With header.");

    public static IResult InvitationInvalid() =>
        Problem(StatusCodes.Status422UnprocessableEntity, "invitation_invalid", "The Invitation is unknown, used or expired.");

    public static IResult SignInLinkInvalid() =>
        Problem(StatusCodes.Status422UnprocessableEntity, "sign_in_link_invalid", "The sign-in link is unknown, used or expired.");

    public static IResult InvalidRequest(string field, string fieldCode) =>
        Problem(
            StatusCodes.Status400BadRequest,
            "invalid_request",
            "The request is malformed.",
            new Dictionary<string, string[]> { [field] = [fieldCode] });

    public static IResult AccountNotFound() =>
        Problem(StatusCodes.Status404NotFound, "account_not_found", "The Member has no Credit Account.");

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
