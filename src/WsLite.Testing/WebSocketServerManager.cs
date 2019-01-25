using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WsLite.Socket;

namespace WsLite.Testing
{
    public sealed class WebSocketServerManager
    {
        public CancellationTokenSource Ct = new CancellationTokenSource();

        public WebSocketServerManager()
        {
            var x = new WebSocketFrame();
        }

        public async Task StartAsync()
        {
            var config = new WebSocketConfig
            {
                Host = "ws://server.example.com",
                RequireOrigin = false,
                UpgradeTimeout = TimeSpan.FromSeconds(10),
                Protocols = new[] { "proto1", "proto2" },
                MaxFrameCount = 1
            };
            var server = new WsLiteServer(config, IPAddress.Parse("127.0.0.1"), 80);

            await server.AcceptConnectionsAsync<TestingHandler>(10, Ct.Token);
        }
    }
}
