using System.Security.Cryptography;
using System.Text;

namespace Mastera.Application.Invitations;

/// <summary>Random, URL-safe invitation secrets. Only their SHA-256 hash is stored.</summary>
public static class InvitationToken
{
    public static string New() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
