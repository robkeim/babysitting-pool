using System.Buffers.Text;
using System.Globalization;
using System.Text;
using BabysittingPool.Application.Credits;

namespace BabysittingPool.Application;

/// <summary>Opaque paging cursor over lists ordered by a timestamp, ties broken by id.</summary>
internal static class TimestampCursor
{
    public static string Encode(DateTime at, Guid id) =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            $"{at.Ticks.ToString(CultureInfo.InvariantCulture)}|{id:N}"));

    public static (DateTime At, Guid Id) Decode(string cursor)
    {
        try
        {
            var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor)).Split('|');
            return (
                new DateTime(long.Parse(parts[0], CultureInfo.InvariantCulture), DateTimeKind.Utc),
                Guid.ParseExact(parts[1], "N"));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or IndexOutOfRangeException)
        {
            throw new InvalidCursorException(ex);
        }
    }
}
