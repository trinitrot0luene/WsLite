using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using WsLite.Http;
using WsLite.Socket;

namespace WsLite
{
    /// <summary>
    /// A wrapper of the underlying network stream which parses protocol-level messages.
    /// </summary>
    public sealed class WsLiteConnection : IDisposable
    {
        private Stream stream { get; }

        private WebSocketConfig config { get; }

        private CancellationToken ct { get; }

        private byte[] messageBuffer { get; }

        /// <summary>
        /// A unique value that can be used to retrieve the connection instance from the <see cref="WsLiteServer"/> it was spawned from.
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Raised when the status of the connection is updated.
        /// </summary>
        public event Func<LogMessage, Task> OnLog;

        /// <summary>
        /// Raised when the connection encounters an error.
        /// </summary>
        public event Func<Exception, Task> OnError;

        /// <summary>
        /// Raised when the connection receives a valid <see cref="WebSocketFrame"/>
        /// </summary>
        public event Func<OpCode, WebSocketCloseStatus?, ArraySegment<byte>, Task> OnMessage;

        /// <param name="id">A unique identifier for the connection.</param>
        /// <param name="config">The server's configuration.</param>
        /// <param name="stream">The stream to exchange frames on.</param>
        internal WsLiteConnection(Guid id, WebSocketConfig config, Stream stream, CancellationToken ct)
        {
            this.Id = id;
            this.stream = stream;
            this.config = config;
            this.ct = ct;

            this.messageBuffer = HttpHandshakeUtility.ArrayPool.Rent(config.BufferSize * config.MaxFrameCount);
        }

        /// <summary>
        /// Send an arbitrary WebSocket frame to the connection.
        /// </summary>
        /// <param name="opCode">The <see cref="OpCode"/> of the frame being sent.</param>
        /// <param name="payload">Bytes to send as part of the payload.</param>
        /// <returns></returns>
        public async Task SendAsync(OpCode opCode, ArraySegment<byte>? payload = default)
        {
            var buffer = HttpHandshakeUtility.ArrayPool.Rent(config.BufferSize);
            var requiredFrames = Math.Ceiling(payload?.Count / (double)config.BufferSize ?? 1);
            for (int i = 0; i < requiredFrames; i++)
            {
                var writtenBytes = WebSocketFrame.WriteToBuffer(buffer, i == (requiredFrames - 1), opCode, false, null, payload);

                await WebSocketFrame.WriteToStreamAsync(stream, new ArraySegment<byte>(buffer, 0, writtenBytes), ct);
            }
            HttpHandshakeUtility.ArrayPool.Return(buffer);
        }

        /// <summary>
        /// Close the connection with an arbitrary <see cref="WebSocketCloseStatus"/>.
        /// </summary>
        /// <param name="closeCode">The close code to send.</param>
        /// <returns></returns>
        public async Task CloseAsync(WebSocketCloseStatus closeCode)
        {
            var closeBytes = BitConverter.GetBytes((ushort)closeCode);
            Array.Reverse(closeBytes);
            await SendAsync(OpCode.Close, new ArraySegment<byte>(closeBytes));
        }

        /// <summary>
        /// Attempt to read a handshake from the underlying stream. This method will block if the handshake is valid.
        /// </summary>
        /// <returns></returns>
        public async Task HandshakeAsync()
        {
            var readHandshake = HttpHandshake.ReadFromStreamAsync(config, stream, ct);
            var abortTask = Task.Delay(config.UpgradeTimeout, ct).ContinueWith(_ => _);

            await Task.WhenAny(readHandshake, abortTask);
            if (!readHandshake.IsCompleted)
            {
                InvokeOnErrored(new TimeoutException("The client timed out before sending any data to the socket."));
                return;
            }

            var handshake = await readHandshake;
            if (!handshake.VerifyIsWebSocketUpgrade())
            {
                var badRequestMessage = HttpHandshakeUtility.GenerateResponseMessage(400);

                await stream.WriteAsync(badRequestMessage, 0, badRequestMessage.Length);
                InvokeOnErrored(new InvalidDataException("400: The handshake was not a valid WebSocket upgrade request."));
                return;
            }

            var acceptString = HttpHandshakeUtility.GetSecWebSocketAcceptString(handshake.Headers["Sec-WebSocket-Key"]);

            var handshakeResponse = HttpHandshakeUtility.GenerateResponseMessage(101, new Dictionary<string, string>()
            {
                {"Upgrade", "websocket"},
                {"Connection", "Upgrade"},
                {"Sec-WebSocket-Accept", acceptString}
            });

            await stream.WriteAsync(handshakeResponse, 0, handshakeResponse.Length);

            await ListenAsync();
        }

        private async Task ListenAsync()
        {
            var buffer = HttpHandshakeUtility.ArrayPool.Rent(config.BufferSize);

            int pos = 0;
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var result = await WebSocketFrame.ReadFromStreamAsync(stream, buffer, ct);

                    // TODO: C#8 Pattern matched tuples for this section
                    if (result.Equals(WebSocketFrame.TimedOut) || result.Equals(WebSocketFrame.InsufficientBuffer))
                    {
                        InvokeOnErrored(result.Item2);
                        break;
                    }
                    (WebSocketFrame frame, WebSocketException ex) = result;

                    if (frame.HasMask)
                        InvokeOnErrored(new WebSocketException("The client sent an unmasked payload."));

                    if (frame.Payload.Array != null)
                        Array.Copy(frame.Payload.Array, frame.Payload.Offset, messageBuffer, pos, frame.Payload.Count);

                    pos += frame.Payload.Count;
                    if (frame.IsFin)
                    {
                        InvokeOnMessage(frame.OpCode, frame.CloseStatus, new ArraySegment<byte>(messageBuffer, 0, pos));
                        pos = 0;
                    }
                }
                catch (Exception ex)
                {
                    InvokeOnErrored(ex);
                    return;
                }
            }
        }

        private void InvokeOnLog(string message, Exception ex = null)
            => OnLog(new LogMessage($"Stream {Id}", message, ex));

        private void InvokeOnErrored(Exception ex)
            => OnError?.Invoke(ex);

        private void InvokeOnMessage(OpCode opCode, WebSocketCloseStatus? status, ArraySegment<byte> buffer)
            => OnMessage?.Invoke(opCode, status, buffer);

        private bool disposedValue = false;

        private void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    HttpHandshakeUtility.ArrayPool.Return(messageBuffer);
                    stream.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose() => Dispose(true);
    }
}