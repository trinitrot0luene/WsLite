using System;
using WsLite.Http;

namespace WsLite
{
    public sealed class WebSocketConfig
    {
        public WebSocketConfig()
        {
            Protocols = null;
            Host = null;
            RequireOrigin = false;
            UpgradeTimeout = TimeSpan.FromSeconds(10);
            BufferSize = 4096;
            MaxFrameCount = 1;
        }

        /// <summary>
        /// Optional protocols that the client can request from the server. Exposed by the initial <see cref="HttpHandshake"/>.
        /// </summary>
        public string[] Protocols { get; set; }

        /// <summary>
        /// How long the server will wait for a client to send a valid upgrade request.
        /// </summary>
        public TimeSpan UpgradeTimeout { get; set; }

        /// <summary>
        /// The host that the client is requesting a connection to.
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// Whether the client is required to send an Origin header in the initial upgrade request. When expecting browser clients, set this to true.
        /// </summary>
        public bool RequireOrigin { get; set; }

        /// <summary>
        /// The size of the buffer that the server will make available to read incoming frames.
        /// </summary>
        public int BufferSize { get; set; }

        /// <summary>
        /// The maximum amount of frames that a client is allowed to send per-message.
        /// </summary>
        public int MaxFrameCount { get; set; }
    }
}