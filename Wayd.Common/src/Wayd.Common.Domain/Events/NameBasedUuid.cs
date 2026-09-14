using System.Security.Cryptography;
using System.Text;

namespace Wayd.Common.Domain.Events;

/// <summary>
/// Version 5 (SHA-1, name-based) UUIDs, as RFC 9562 section 5.5 defines them: the same namespace and name
/// always produce the same id.
/// </summary>
public static class NameBasedUuid
{
    public static Guid Create(Guid namespaceId, string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[16 + nameBytes.Length];
        namespaceId.TryWriteBytes(input, bigEndian: true, out _);
        nameBytes.CopyTo(input, 16);

        // SHA-1 is what the version 5 layout specifies; nothing here relies on it resisting collisions.
#pragma warning disable CA5350
        var hash = SHA1.HashData(input);
#pragma warning restore CA5350

        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }
}
