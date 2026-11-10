using System.Globalization;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace BabysittingPool.Tests.Support;

/// <summary>An in-memory Serilog sink: register it as an <see cref="ILogEventSink"/> and read what the app logged.</summary>
internal sealed class CapturedLogEvents : ILogEventSink
{
    private readonly object _gate = new();
    private readonly List<LogEvent> _events = [];

    public IReadOnlyList<LogEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return [.. _events];
            }
        }
    }

    public void Emit(LogEvent logEvent)
    {
        lock (_gate)
        {
            _events.Add(logEvent);
        }
    }

    /// <summary>Every event as JSON (properties, template, exception) followed by its rendered message.</summary>
    public string AllText()
    {
        using var writer = new StringWriter();
        var formatter = new CompactJsonFormatter();
        foreach (var logEvent in Events)
        {
            formatter.Format(logEvent, writer);
            writer.WriteLine(logEvent.RenderMessage(CultureInfo.InvariantCulture));
        }

        return writer.ToString();
    }

    public LogEvent RequestLine(string routeTemplate) =>
        Events.Single(e => e.MessageTemplate.Text.StartsWith("HttpRequestHandled", StringComparison.Ordinal)
            && Text(e, "RouteTemplate") == routeTemplate);

    public static string? Text(LogEvent logEvent, string property) =>
        logEvent.Properties.TryGetValue(property, out var value) && value is ScalarValue { Value: { } scalar } ? scalar.ToString() : null;
}
