namespace BabysittingPool.Api;

/// <summary>
/// Stand-in for authentication until it is built: outside Production the caller names the current Member
/// in the <c>X-Dev-Member-Id</c> header.
/// </summary>
internal static class DevIdentity
{
    public const string HeaderName = "X-Dev-Member-Id";

    /// <summary>The Member seeded at startup in Development.</summary>
    public static readonly Guid DevMemberId = Guid.Parse("00000000-0000-0000-0000-00000000de01");

    public static Guid? CurrentMemberId(HttpContext context, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment("E2E")) return null;
        return Guid.TryParse(context.Request.Headers[HeaderName], out var id) ? id : null;
    }
}
