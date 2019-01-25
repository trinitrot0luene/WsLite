using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace WsLite.Socket
{
    public class WebSocketFrame
    {
        public OpCode OpCode { get; private set; }

        public WebSocketCloseStatus? CloseStatus { get; private set; }

        public bool IsFin { get; private set; }

        public bool HasRsv1 { get; private set; }

        public bool HasRsv2 { get; private set; }

        public bool HasRsv3 { get; private set; }

        public bool HasMask { get; private set; }

        public int Length { get; private set; }

        public int Offset { get; private set; }

        public ArraySegment<byte> Payload { get; private set; }

        public static (WebSocketFrame, WebSocketException) TimedOut 
            = (default, new WebSocketException(WebSocketError.Faulted, "The rest of the expected frame did not arrive in time."));

        public static (WebSocketFrame, WebSocketException) InsufficientBuffer
            = (default, new WebSocketException(WebSocketError.Faulted, "The payload was too large to read into the provided buffer."));

        public static async Task<(WebSocketFrame, WebSocketException)> ReadFromStreamAsync(Stream source, byte[] buffer, CancellationToken ct)
        {
            var frame = new WebSocketFrame();

            var read = await source.ReadAsync(buffer, 0, 1, ct);
            if (read == 0)
                return TimedOut;
            frame.Offset += read;

            frame.IsFin = (buffer[0] & 0x80) == 0x80;
            frame.HasRsv1 = (buffer[0] & 0x40) == 0x40;
            frame.HasRsv2 = (buffer[0] & 0x20) == 0x20;
            frame.HasRsv3 = (buffer[0] & 0x10) == 0x10;
            frame.OpCode = (OpCode)(buffer[0] & 0x0F);

            read = await source.ReadAsync(buffer, frame.Offset, 1, ct);
            if (read == 0)
                return TimedOut;
            frame.Offset += read;

            frame.HasMask = (buffer[1] & 0x80) == 0x80;

            var length = (uint)(buffer[1] & 0x7F);

            if (length == 126)
            {
                read = await source.ReadAsync(buffer, frame.Offset, 2, ct);
                if (read == 0)
                    return TimedOut;
                frame.Offset += read;

                Array.Reverse(buffer, 2, 2);
                frame.Length = BitConverter.ToUInt16(buffer, 2);
            }
            else if (length == 127)
            {
                read = await source.ReadAsync(buffer, frame.Offset, 8, ct);
                if (read == 0)
                    return TimedOut;
                frame.Offset += read;

                Array.Reverse(buffer, 2, 8);
                frame.Length = (int)BitConverter.ToUInt64(buffer, 2);
            }

            frame.Length = (int)length;

            byte[] keyBuffer = default;
            if (frame.HasMask)
            {
                keyBuffer = new byte[4];
                read = await source.ReadAsync(keyBuffer, 0, 4, ct);
                if (read == 0)
                    return TimedOut;
            }

            if (frame.Offset + frame.Length > buffer.Length)
                return InsufficientBuffer;
            if (frame.Length == 0)
                return (frame, default);

            read = await source.ReadAsync(buffer, frame.Offset, frame.Length, ct);
            if (read == 0)
                return TimedOut;

            frame.Payload = new ArraySegment<byte>(buffer, frame.Offset, frame.Length);

            if (frame.HasMask)
                for (int i = frame.Payload.Offset; i < frame.Payload.Count + frame.Offset; i++)
                    buffer[i] = (byte)(buffer[i] ^ keyBuffer[(i - frame.Payload.Offset) % 4]);

            if (frame.OpCode == OpCode.Close)
            {
                Array.Reverse(buffer, frame.Payload.Offset, 2);
                frame.CloseStatus = (WebSocketCloseStatus)BitConverter.ToUInt16(buffer, frame.Payload.Offset);
            }

            return (frame, default);
        }

        public static Task WriteToStreamAsync(Stream destination, ArraySegment<byte> frame, CancellationToken ct)
         => destination.WriteAsync(frame.Array, frame.Offset, frame.Count, ct);

        public static int WriteToBuffer(byte[] buffer, bool isFin, OpCode opCode, bool isMasked, byte[] key, ArraySegment<byte>? payload)
        {
            int offset = 0;

            buffer[offset] |= (byte)(isFin ? 0x80 : 0);
            buffer[offset] |= (byte)opCode;
            offset++;

            buffer[offset] |= (byte)(isMasked ? 0x80 : 0);

            if (payload?.Count < 126)
                buffer[offset] = (byte)(payload?.Count ?? 0);
            else
            {
                buffer[offset] = (byte)(payload.Value.Count <= 65535 ? 126 : 127);
                var bytes = BitConverter.GetBytes(payload.Value.Count);
                for (int i = 0; i < bytes.Length; i++)
                    buffer[i + 2] |= bytes[i];
                offset += bytes.Length;
            }

            if (isMasked)
            {
                if (key.Length != 4)
                    throw new ArgumentException("Key must be exactly 4 bytes in length.");
                for (int i = 0; i < key.Length; i++)
                    buffer[++offset] = key[i];
            }

            for (int i = payload?.Offset ?? 0; i < (payload?.Count ?? 0); i++)
                buffer[++offset] = payload.Value.Array[i];

            return offset + 1;
        }
    }
}
