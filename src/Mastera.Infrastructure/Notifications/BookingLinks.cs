using System.Security.Cryptography;
using System.Text;
using Mastera.Application.Notifications;
using Mastera.Domain.Bookings;
using Mastera.Domain.Tenants;
using Mastera.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace Mastera.Infrastructure.Notifications;

/// <summary>
/// Task 5.6: "manage booking" tokens are the booking id plus an HMAC-SHA256 signature, so nothing secret is stored
/// and the same link can be put in every SMS and reminder. A changed character makes the token invalid.
/// </summary>
public sealed class BookingLinks(IOptions<AppLinkOptions> options) : IBookingLinks
{
    private const int SignatureBytes = 16;

    public string CreateManageToken(BookingId bookingId)
    {
        var id = bookingId.Value.ToByteArray();
        var token = new byte[id.Length + SignatureBytes];
        id.CopyTo(token, 0);
        Sign(id).AsSpan(0, SignatureBytes).CopyTo(token.AsSpan(id.Length));
        return Base64Url(token);
    }

    public BookingId? ReadManageToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 64)
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(token.Replace('-', '+').Replace('_', '/') + new string('=', (4 - (token.Length % 4)) % 4));
        }
        catch (FormatException)
        {
            return null;
        }

        if (bytes.Length != 16 + SignatureBytes)
        {
            return null;
        }

        var id = bytes.AsSpan(0, 16).ToArray();
        var expected = Sign(id).AsSpan(0, SignatureBytes);
        return CryptographicOperations.FixedTimeEquals(expected, bytes.AsSpan(16)) ? new BookingId(new Guid(id)) : null;
    }

    public string ManageUrl(TenantSlug salon, string token)
    {
        ArgumentNullException.ThrowIfNull(salon);
        var site = options.Value.BookingSiteUrl.Replace("{slug}", salon.Value, StringComparison.Ordinal).TrimEnd('/');
        return $"{site}/manage/{token}";
    }

    private byte[] Sign(byte[] data) => HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.Value.LinkSecret), data);

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
