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

    public static IResult InvitationNotFound() =>
        Problem(StatusCodes.Status404NotFound, "invitation_not_found", "There is no such Invitation.");

    public static IResult InvitationAlreadyAccepted() =>
        Problem(StatusCodes.Status422UnprocessableEntity, "invitation_already_accepted", "The Invitation was already accepted.");

    public static IResult InvitationAlreadySent() =>
        Problem(
            StatusCodes.Status422UnprocessableEntity,
            "invitation_already_sent",
            "This address already has an Invitation that was not accepted; re-send it instead.");

    public static IResult AlreadyAMember() =>
        Problem(StatusCodes.Status422UnprocessableEntity, "already_a_member", "This address already belongs to a Member.");

    public static IResult SignInLinkInvalid() =>
        Problem(StatusCodes.Status422UnprocessableEntity, "sign_in_link_invalid", "The sign-in link is unknown, used or expired.");

    public static IResult TooManyRequests() =>
        Problem(StatusCodes.Status429TooManyRequests, "rate_limited", "Too many requests; try again later.");

    public static IResult PayloadTooLarge() =>
        Problem(StatusCodes.Status413PayloadTooLarge, "payload_too_large", "The request body is too large.");

    public static IResult AddressServiceUnavailable() =>
        Problem(StatusCodes.Status503ServiceUnavailable, "address_service_unavailable", "The address service is unavailable; try again later.");

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
            exception.Code == DomainException.InvalidStateCode ? StatusCodes.Status409Conflict : StatusCodes.Status422UnprocessableEntity,
            exception.Code,
            exception.Message);

    public static IResult Conflict() =>
        Problem(StatusCodes.Status409Conflict, "concurrent_update", "The state changed meanwhile; try again.");

    public static IResult AccountNotFound() =>
        Problem(StatusCodes.Status404NotFound, "account_not_found", "The Member has no Credit Account.");

    public static IResult MemberNotFound() =>
        Problem(StatusCodes.Status404NotFound, "member_not_found", "There is no such Member.");

    public static IResult NotificationNotFound() =>
        Problem(StatusCodes.Status404NotFound, "notification_not_found", "There is no such Notification.");

    public static IResult FeatureOff() =>
        Problem(StatusCodes.Status404NotFound, "feature_off", "This feature is not available.");

    public static IResult FeatureFlagNotFound() =>
        Problem(StatusCodes.Status404NotFound, "feature_flag_not_found", "There is no such feature flag.");

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
