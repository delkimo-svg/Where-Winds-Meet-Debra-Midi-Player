using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;

namespace WhereWindsMeetMidiPlayer.Services.Discord;

/// <summary>
/// Shared HTTP client for Discord REST API and CDN downloads (User-Agent avoids Cloudflare 40333).
/// </summary>
internal static class DiscordApiHttp
{
    public const string UserAgent = "DiscordBot (https://github.com/delkimo-svg/Where-Winds-Meet-Debra-Midi-Player, 1.0.0)";

    public static HttpClient Create(TimeSpan? timeout = null, bool preferIpv4 = true)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(60),
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            SslOptions = new SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }
        };

        if (preferIpv4)
            handler.ConnectCallback = ConnectPreferIpv4Async;

        var client = new HttpClient(handler) { Timeout = timeout ?? TimeSpan.FromMinutes(15) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
        return client;
    }

    /// <summary>System default DNS/connect — fallback when IPv4-first downloads fail with SSL/reset errors.</summary>
    public static HttpClient CreateFallback(TimeSpan? timeout = null) => Create(timeout, preferIpv4: false);

    private static async ValueTask<Stream> ConnectPreferIpv4Async(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken)
            .ConfigureAwait(false);
        if (addresses.Length == 0)
            throw new HttpRequestException($"Could not resolve host '{context.DnsEndPoint.Host}'.");

        foreach (var address in addresses.OrderBy(static a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1))
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken)
                    .ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
            }
        }

        throw new HttpRequestException($"Could not connect to '{context.DnsEndPoint.Host}'.");
    }
}
