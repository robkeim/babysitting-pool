using System.Security.Cryptography;
using System.Text;

namespace BabysittingPool.Application.Identity;

/// <summary>Random link and cookie secrets. Only the hash is stored (ADR 0012).</summary>
public static class Secrets
{
    public static string Generate() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string secret) => Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
