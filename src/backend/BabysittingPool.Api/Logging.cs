using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace BabysittingPool.Api;

/// <summary>Serilog as the <c>ILogger</c> provider, JSON to stdout, one line per request (ADR 0010).</summary>
internal static class Logging
{
    public const string RequestTemplate = "HttpRequestHandled {RequestMethod} {RouteTemplate} {StatusCode} {ElapsedMs}";

    /// <summary>What the line says when no endpoint matched: never the raw path, which can hold ids and typos of tokens.</summary>
    public const string UnmatchedRoute = "unmatched";

    private const string RouteTemplateItemKey = "babysitting.route-template";

    public static void AddCoopLogging(this WebApplicationBuilder builder)
    {
        // The host would add the activity's own TraceId to every event, which beats ours; the trace id is ours (ADR 0010).
        builder.Logging.Configure(options => options.ActivityTrackingOptions = ActivityTrackingOptions.None);
        builder.Host.UseSerilog(
            (context, services, logger) =>
            {
                logger.ReadFrom.Configuration(context.Configuration)
                    .Enrich.FromLogContext()
                    .Enrich.With<HostingScopeScrubEnricher>()
                    .Enrich.With<DenylistMaskingEnricher>()
                    .WriteTo.Console(new CompactJsonFormatter());
                // Tests register a sink here to read what the app logs.
                foreach (var sink in services.GetServices<ILogEventSink>())
                {
                    logger.WriteTo.Sink(sink);
                }
            },
            // Hosts built side by side (the test suite) must not share or replace the static logger.
            preserveStaticLogger: true);
    }

    public static IApplicationBuilder UseCoopRequestLogging(this WebApplication app)
    {
        // The exception handler clears the endpoint it finds, which would turn a handled failure into "unmatched".
        app.Use((context, next) =>
        {
            context.Items[RouteTemplateItemKey] = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? UnmatchedRoute;
            return next(context);
        });

        return app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = RequestTemplate;
            options.Logger = app.Services.GetRequiredService<Serilog.ILogger>();
            // The default properties include the raw path; these are the only ones the line gets.
            options.GetMessageTemplateProperties = (context, _, elapsedMs, statusCode) =>
            [
                new LogEventProperty("RequestMethod", new ScalarValue(context.Request.Method)),
                new LogEventProperty("RouteTemplate", new ScalarValue(RouteTemplate(context))),
                new LogEventProperty("StatusCode", new ScalarValue(statusCode)),
                new LogEventProperty("ElapsedMs", new ScalarValue(Math.Round(elapsedMs, 1))),
            ];
            // Expected 4xx outcomes are not errors (ADR 0006).
            options.GetLevel = (context, _, exception) =>
                exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError
                    ? LogEventLevel.Error
                    : LogEventLevel.Information;
            // Authentication runs below this middleware, so the scope cannot carry the Member; the line asks afterwards.
            options.EnrichDiagnosticContext = (diagnostics, context) =>
            {
                if (context.MemberId() is { } memberId)
                {
                    diagnostics.Set("MemberId", memberId);
                }
            };
        });
    }

    private static string RouteTemplate(HttpContext context) =>
        context.Items.TryGetValue(RouteTemplateItemKey, out var template) && template is string text ? text : UnmatchedRoute;
}
