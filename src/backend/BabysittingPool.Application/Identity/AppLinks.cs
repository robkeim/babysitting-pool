namespace BabysittingPool.Application.Identity;

/// <summary>Where the SPA is served from; the origin that links in emails point at (deployment configuration).</summary>
public sealed record AppLinks(Uri BaseUrl)
{
    public Uri Invitation(string secret) => Build("invitation", secret);

    public Uri SignIn(string secret) => Build("sign-in/verify", secret);

    private Uri Build(string path, string secret) =>
        new(BaseUrl, $"{path}?token={Uri.EscapeDataString(secret)}");
}
