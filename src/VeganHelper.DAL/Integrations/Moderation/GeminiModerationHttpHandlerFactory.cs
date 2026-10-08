using System.Net;
using System.Net.Sockets;

namespace VeganHelper.DAL.Integrations.Moderation;

/// <summary>Use for the moderation typed HttpClient so media DNS is checked at socket creation.</summary>
public static class GeminiModerationHttpHandlerFactory
{
    public static SocketsHttpHandler Create() => new()
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.None,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectCallback = ConnectPublicAddressAsync
    };

    private static async ValueTask<Stream> ConnectPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
        // Reject mixed public/private responses as well as wholly private DNS responses.
        if (addresses.Length == 0 || addresses.Any(address => !IsPublicAddress(address)))
            throw new ModerationAiException("unsafe_media_url", false);
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                // Connect to this exact checked address; do not resolve the hostname a second time.
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException)
            {
                socket.Dispose();
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
        throw new HttpRequestException("Public host is unavailable.");
    }

    private static bool IsPublicAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return false;
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] is not (0 or 10 or 127) && bytes[0] < 224 &&
                !(bytes[0] == 100 && bytes[1] is >= 64 and <= 127) &&
                !(bytes[0] == 169 && bytes[1] == 254) &&
                !(bytes[0] == 172 && bytes[1] is >= 16 and <= 31) &&
                !(bytes[0] == 192 && (bytes[1] == 168 ||
                    (bytes[1] == 0 && bytes[2] is 0 or 2) || (bytes[1] == 88 && bytes[2] == 99))) &&
                !(bytes[0] == 198 && (bytes[1] is 18 or 19 || (bytes[1] == 51 && bytes[2] == 100))) &&
                !(bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113);
        }
        if (address.AddressFamily != AddressFamily.InterNetworkV6 || address.ScopeId != 0)
            return false;
        // Only global unicast; exclude transition and special-purpose/documentation prefixes.
        return (bytes[0] & 0xe0) == 0x20 &&
            !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] < 2) &&
            !(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8) &&
            !(bytes[0] == 0x20 && bytes[1] == 0x02);
    }
}
