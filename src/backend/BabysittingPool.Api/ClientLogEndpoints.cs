using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Serilog.Context;

namespace BabysittingPool.Api;

/// <summary>
/// The SPA's error reports (ADR 0010): a fixed schema of short, charset-checked fields, written into the backend
/// log with <c>Source=Frontend</c>. The body is read by hand so an oversized, malformed or unexpected payload is
/// rejected and logged at Warning without its content ever reaching the log.
/// </summary>
internal static partial class ClientLogEndpoints
{
    public const string Path = "/api/client-logs";

    public const string FrontendSource = "Frontend";

    /// <summary>A batch of a few fields is well under 1 KiB; the global 256 KiB cap (ADR 0014) is far too generous here.</summary>
    public const int MaxBodyBytes = 4 * 1024;

    public const int MaxMessageLength = 500;

    private const int MaxCodeLength = 64;

    private const int MaxRouteLength = 100;

    private const int MaxVersionLength = 32;

    private const string TraceIdPattern = "^[0-9a-f]{32}$";

    public static void MapClientLogEndpoints(this IEndpointRouteBuilder app) =>
        app.MapPost(Path, async (HttpContext context, ILoggerFactory loggers, CancellationToken cancellationToken) =>
        {
            var logger = loggers.CreateLogger("BabysittingPool.Api.ClientLogs");
            var (report, rejection) = await ReadAsync(context.Request.Body, cancellationToken);
            if (report is null)
            {
                logger.LogWarning("ClientLogRejected {Field} {Reason}", rejection!.Value.Field, rejection.Value.Reason);
                return rejection.Value.Reason == "too_large"
                    ? Problems.PayloadTooLarge()
                    : Problems.InvalidRequest(rejection.Value.Field, rejection.Value.Reason);
            }

            // The report's own trace id replaces the request's, so `TraceId = X` finds the failed call's server lines too.
            using (LogContext.PushProperty("TraceId", report.TraceId ?? context.TraceId()))
            using (LogContext.PushProperty("Source", FrontendSource))
            {
                logger.LogError(
                    "ClientErrorReported {ErrorCode} {Route} {AppVersion} {ErrorMessage}",
                    report.Code, report.Route, report.AppVersion, report.Message);
            }

            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("ReportClientError")
        .Accepts<ClientLogRequest>("application/json")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
        .ProducesProblem(StatusCodes.Status429TooManyRequests);

    [GeneratedRegex("^[A-Za-z0-9_.-]+$")]
    private static partial Regex CodeCharacters();

    [GeneratedRegex("^[A-Za-z0-9_./:-]+$")]
    private static partial Regex RouteCharacters();

    [GeneratedRegex(@"^[A-Za-z0-9_.+-]+$")]
    private static partial Regex VersionCharacters();

    [GeneratedRegex(TraceIdPattern)]
    private static partial Regex TraceIdCharacters();

    private static async Task<(ClientLogRequest? Report, (string Field, string Reason)? Rejection)> ReadAsync(
        Stream body, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[1024];
        int read;
        while ((read = await body.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > MaxBodyBytes)
            {
                return (null, ("body", "too_large"));
            }
        }

        ClientLogRequest? report;
        try
        {
            report = JsonSerializer.Deserialize<ClientLogRequest>(buffer.ToArray(), JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return (null, ("body", "malformed"));
        }

        return report is null ? (null, ("body", "malformed")) : (Validate(report) is { } rejection ? (null, rejection) : (report, null));
    }

    private static (string Field, string Reason)? Validate(ClientLogRequest report)
    {
        if (!IsText(report.Message, MaxMessageLength))
        {
            return ("message", "invalid");
        }

        if (!IsMatch(report.Code, MaxCodeLength, CodeCharacters()))
        {
            return ("code", "invalid");
        }

        if (!IsMatch(report.Route, MaxRouteLength, RouteCharacters()))
        {
            return ("route", "invalid");
        }

        // Not every error happens inside an API call, so there may be no trace id to quote.
        if (report.TraceId is not null && !TraceIdCharacters().IsMatch(report.TraceId))
        {
            return ("traceId", "invalid");
        }

        return IsMatch(report.AppVersion, MaxVersionLength, VersionCharacters()) ? null : ("appVersion", "invalid");
    }

    /// <summary>Non-blank, within the length, and no control, format or line-separator characters, so a message cannot forge or disguise a log line.</summary>
    private static bool IsText(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength && !value.Any(IsInvisible);

    private static bool IsInvisible(char c) =>
        char.IsControl(c) || char.GetUnicodeCategory(c) is UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;

    private static bool IsMatch(string? value, int maxLength, Regex characters) =>
        !string.IsNullOrEmpty(value) && value.Length <= maxLength && characters.IsMatch(value);

    /// <summary>Any property outside this list rejects the payload: the endpoint must not become a free-form channel.</summary>
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    internal sealed record ClientLogRequest(string? Message, string? Code, string? Route, string? TraceId, string? AppVersion);
}
