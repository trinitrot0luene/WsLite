using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using WsLite.Handlers;

namespace WsLite
{
    /// <summary>
    /// A WebSocket server implementation.
    /// </summary>
    public sealed class WsLiteServer
    {
        private readonly WebSocketConfig config;

        private readonly TcpListener tcp;

        private readonly X509Certificate cert;

        private SemaphoreSlim limiter;

        private List<WsLiteConnection> clients = new List<WsLiteConnection>();

        /// <summary>
        /// Create a new WebSocket server.
        /// </summary>
        /// <param name="config">The configuration the server will use.</param>
        /// <param name="ipAddress">The IP address the server should listen on.</param>
        /// <param name="port">The port the server should listen on.</param>
        /// <param name="cert">The X509 certificate the server should use (automatically enforces SSL)</param>
        public WsLiteServer(WebSocketConfig config, IPAddress ipAddress, int port, X509Certificate cert = null)
        {
            tcp = new TcpListener(ipAddress, port);

            this.config = config;
            this.cert = cert;
        }

        /// <summary>
        /// Block and asynchronously accept new WebSocket connections.
        /// </summary>
        /// <param name="limit">The maximum amount of clients the server will accept.</param>
        /// <param name="ct">A cancellation token that when cancelled, stops new clients from being accepted, </param>
        /// <param name="services">Services used to inject dependencies into connection handlers.</param>
        /// <returns></returns>
        public async Task AcceptConnectionsAsync<THandler>(int limit, CancellationToken ct, IServiceProvider services = null)
            where THandler : HandlerBase<HandlerContext>
        {
            limiter = new SemaphoreSlim(limit);

            try
            {
                tcp.Start();
                while (!ct.IsCancellationRequested)
                {
                    await limiter.WaitAsync().ConfigureAwait(false);

                    var networkStream = await GetNetworkStreamAsync(tcp).ConfigureAwait(false);

                    var connection = new WsLiteConnection(Guid.NewGuid(), config, networkStream, ct);

                    clients.Add(connection);

                    var handler = Activator.CreateInstance<THandler>();
                    handler.SetContext(new HandlerContext(this, connection, services));
                    connection.OnMessage += handler.HandleMessageAsync;
                    connection.OnLog += handler.HandleLogAsync;
                    connection.OnError += handler.HandleErrorAsync;

                    #pragma warning disable CS4014
                    connection.HandshakeAsync().ConfigureAwait(false);
                    #pragma warning restore CS4014
                }
            }
            finally
            {
                foreach (var client in clients)
                    client.Dispose();
                tcp.Stop();
            }
        }

        /// <summary>
        /// Remove the connection from the clients list, and dispose the underlying stream.
        /// </summary>
        /// <param name="connection"></param>
        public void Disconnect(WsLiteConnection connection)
        {
            using (connection)
                clients.Remove(connection);
        }

        private async Task<Stream> GetNetworkStreamAsync(TcpListener listener)
        {
            var client = listener.AcceptTcpClient();

            var ns = cert == null ? (Stream)client.GetStream() : new SslStream(client.GetStream(), false);
            if (ns is SslStream sslStream)
                await sslStream.AuthenticateAsServerAsync(cert, false, SslProtocols.Default, true).ConfigureAwait(false);

            return ns;
        }
    }
}