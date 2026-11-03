using System.Runtime.CompilerServices;

namespace BabysittingPool.Tests.Support;

/// <summary>
/// Development hosts load <c>dotnet user-secrets</c> from <c>$HOME/.microsoft/usersecrets</c>. Pointing HOME at an
/// empty directory keeps a developer's real SMTP secrets out of every test host, so tests never send email
/// (ADR 0011).
/// </summary>
internal static class NoUserSecrets
{
#pragma warning disable CA2255 // A test assembly has no entry point to run this from.
    [ModuleInitializer]
    internal static void Isolate() =>
        Environment.SetEnvironmentVariable("HOME", Directory.CreateTempSubdirectory("babysitting-home-").FullName);
#pragma warning restore CA2255
}
