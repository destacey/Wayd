using System.Net;
using System.Net.Sockets;

namespace Wayd.Infrastructure.RateLimiting;

/// <summary>Turns a caller's address into the key its per-address rate limits are counted under.</summary>
internal static class ClientAddressKey
{
    /// <summary>The key for a request whose address is unknown, such as one from an in-process test server.</summary>
    internal const string Unknown = "unknown";

    /// <summary>
    /// An IPv4 address as itself, and an IPv6 address as its /64, the block one subscriber is usually given, so a
    /// caller cannot get a fresh window by changing the low bits of its address. An IPv4-mapped IPv6 address counts as
    /// the IPv4 address it carries, so one caller is never counted under two keys.
    /// </summary>
    internal static string For(IPAddress? address)
    {
        if (address is null)
            return Unknown;

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return address.ToString();

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return $"{new IPAddress(bytes)}/64";
    }
}
