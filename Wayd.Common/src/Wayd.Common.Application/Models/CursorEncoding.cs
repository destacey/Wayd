using System.Text;

namespace Wayd.Common.Application.Models;

/// <summary>
/// Turns a list's position into the opaque string a <see cref="CursorPage{T}"/> hands out, and back.
/// </summary>
/// <remarks>
/// The one encoding for every cursor, so each list declares only the keys it is ordered by. The string is
/// URL-safe base64 of the parts joined by <c>:</c>; callers must treat it as opaque, which leaves the format
/// free to change.
/// </remarks>
public static class CursorEncoding
{
    /// <summary>Longer than any cursor the API hands out; anything longer is refused before decoding.</summary>
    private const int MaxLength = 256;

    /// <summary>Encodes the parts of a position.</summary>
    /// <exception cref="ArgumentException">A part contains <c>:</c>, which separates the parts.</exception>
    public static string Encode(params string[] parts)
    {
        if (parts.Any(p => p.Contains(':')))
        {
            throw new ArgumentException("A cursor part cannot contain ':'.", nameof(parts));
        }

        var raw = string.Join(':', parts);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// Decodes a cursor into exactly <paramref name="count"/> parts, or null for anything the API did not hand out.
    /// </summary>
    public static string[]? Decode(string? value, int count)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return null;
        }

        try
        {
            var base64 = value.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split(':');
            return parts.Length == count ? parts : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
