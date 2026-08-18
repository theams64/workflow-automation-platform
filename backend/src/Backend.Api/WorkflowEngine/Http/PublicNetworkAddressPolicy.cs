using System.Net;
using System.Net.Sockets;

namespace Backend.Api.WorkflowEngine.Http
{
    public static class PublicNetworkAddressPolicy
    {
        public static bool IsAllowed(IPAddress address)
        {
            ArgumentNullException.ThrowIfNull(address);

            if (address.IsIPv4MappedToIPv6)
            {
                return IsAllowed(address.MapToIPv4());
            }

            if (IPAddress.IsLoopback(address))
            {
                return false;
            }

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes = address.GetAddressBytes();
                var first = bytes[0];
                var second = bytes[1];
                var third = bytes[2];

                if (first is 0 or 10 or 127) return false;
                if (first == 100 && second is >= 64 and <= 127) return false;
                if (first == 169 && second == 254) return false;
                if (first == 172 && second is >= 16 and <= 31) return false;
                if (first == 192 && second == 168) return false;
                if (first == 192 && second == 0 && third == 0) return false;
                if (first == 192 && second == 0 && third == 2) return false;
                if (first == 192 && second == 88 && third == 99) return false;
                if (first == 198 && second is 18 or 19) return false;
                if (first == 198 && second == 51 && third == 100) return false;
                if (first == 203 && second == 0 && third == 113) return false;

                return first < 224;
            }

            if (address.AddressFamily != AddressFamily.InterNetworkV6)
            {
                return false;
            }

            if (address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6None) || address.Equals(IPAddress.IPv6Loopback) || address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal)
            {
                return false;
            }

            var ipv6 = address.GetAddressBytes();

            // Level 1 allows only ordinary IPv6 global-unicast space (2000::/3).
            // This conservatively rejects transition/local-use prefixes such as
            // IPv4-compatible, NAT64 well-known, discard-only, ULA, and multicast.
            if ((ipv6[0] & 0xE0) != 0x20) return false;

            // 2001::/23 contains special-purpose assignments such as Teredo.
            if (ipv6[0] == 0x20 && ipv6[1] == 0x01 && ipv6[2] <= 0x01)
            {
                return false;
            }

            if (ipv6[0] == 0x20 && ipv6[1] == 0x01 && ipv6[2] == 0x0D && ipv6[3] == 0xB8)
            {
                return false; // 2001:db8::/32 documentation
            }

            if (ipv6[0] == 0x20 && ipv6[1] == 0x02)
            {
                return false; // 2002::/16 6to4
            }

            return true;
        }
    }
}
