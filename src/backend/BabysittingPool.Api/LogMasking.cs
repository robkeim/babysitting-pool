using Serilog.Core;
using Serilog.Events;

namespace BabysittingPool.Api;

/// <summary>
/// The backstop of the never-log list (ADR 0010): a property whose name is, or ends with, a denylisted word is
/// logged as <see cref="Mask"/>, whatever its value and however deeply it is nested. Call sites still must not
/// log these values; this catches the one that does.
/// </summary>
internal sealed class DenylistMaskingEnricher : ILogEventEnricher
{
    public const string Mask = "***";

    /// <summary>Anything about children, tokens and secrets, addresses and names, and free text. Matched at the end of a name.</summary>
    public static readonly string[] Denylist =
    [
        "Token", "Secret", "Password", "Authorization", "Cookie", "Csrf", "Email", "Address", "DisplayName", "Link",
        "Note", "Notes", "Instructions", "Body", "Child", "Children",
    ];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        foreach (var property in logEvent.Properties.ToArray())
        {
            if (IsDenied(property.Key))
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, new ScalarValue(Mask)));
            }
            else if (Scrub(property.Value) is { } scrubbed)
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(property.Key, scrubbed));
            }
        }
    }

    public static bool IsDenied(string name) =>
        Denylist.Any(word => name.EndsWith(word, StringComparison.OrdinalIgnoreCase));

    /// <summary>The value with denylisted members masked, or null when nothing in it needed masking.</summary>
    private static LogEventPropertyValue? Scrub(LogEventPropertyValue value)
    {
        switch (value)
        {
            case StructureValue structure:
                var members = structure.Properties
                    .Select(p => IsDenied(p.Name) ? new LogEventProperty(p.Name, new ScalarValue(Mask)) : Scrub(p.Value) is { } v ? new LogEventProperty(p.Name, v) : p)
                    .ToArray();
                return members.SequenceEqual(structure.Properties) ? null : new StructureValue(members, structure.TypeTag);
            case DictionaryValue dictionary:
                var entries = dictionary.Elements
                    .Select(e => KeyValuePair.Create(
                        e.Key,
                        e.Key.Value is string key && IsDenied(key) ? new ScalarValue(Mask) : Scrub(e.Value) ?? e.Value))
                    .ToArray();
                return entries.SequenceEqual(dictionary.Elements) ? null : new DictionaryValue(entries);
            case SequenceValue sequence:
                var items = sequence.Elements.Select(e => Scrub(e) ?? e).ToArray();
                return items.SequenceEqual(sequence.Elements) ? null : new SequenceValue(items);
            default:
                return null;
        }
    }
}

/// <summary>
/// Drops what ASP.NET's own request scope puts on every event: <c>RequestPath</c> is the raw path, which the
/// never-log list forbids, and <c>RequestId</c> and <c>ConnectionId</c> are noise next to <c>TraceId</c>.
/// </summary>
internal sealed class HostingScopeScrubEnricher : ILogEventEnricher
{
    private static readonly string[] Dropped = ["RequestPath", "RequestId", "ConnectionId"];

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        foreach (var name in Dropped)
        {
            logEvent.RemovePropertyIfPresent(name);
        }
    }
}
