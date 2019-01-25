using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WsLite.Http
{
    /// <summary>
    /// Exposes information about WebSocket upgrade requests sent over HTTP.
    /// </summary>
    public readonly struct HttpHandshake
    {
        public Uri Url { get; }

        public HttpMethod Method { get; }

        public float ProtocolVersion { get; }

        public Dictionary<string, string> Headers { get; }

        public bool VerifyIsWebSocketUpgrade()
        {
            if (Headers.ContainsKey("Host")
                && Headers.TryGetValue("Upgrade", out var uVal) && uVal.Equals("websocket", StringComparison.OrdinalIgnoreCase)
                && Headers.TryGetValue("Connection", out var cVal) && cVal.Equals("upgrade", StringComparison.OrdinalIgnoreCase)
                && Headers.TryGetValue("Sec-WebSocket-Key", out var kVal) && Convert.FromBase64String(kVal).Length == 16
                && Headers.TryGetValue("Sec-WebSocket-Version", out var vVal) && vVal == "13")
                return true;
            else
                return false;
        }

        private HttpHandshake(HttpMethod method, float protocolVersion, Uri url, Dictionary<string, string> headers)
        {
            Url = url;
            Method = method;
            Headers = headers;
            ProtocolVersion = protocolVersion;
        }

        #region Static Members

        private static int _bufferSize = 0x2000;

        private static async Task<string> GetRawRequestFromStreamAsync(Stream connection, CancellationToken token)
        {
            var buffer = HttpHandshakeUtility.ArrayPool.Rent(_bufferSize);

            var bPosition = 0x0;
            var bRead = 0x0;

            do
            {
                bRead = await connection.ReadAsync(buffer, bPosition, _bufferSize - bPosition, token);

                bPosition += bRead;

                if (bPosition > _bufferSize)
                    throw new InsufficientMemoryException("The inbound request size exceeded the available buffer size.");

                if (bPosition > 0x3
                && (HttpHandshakeUtility.CRLF_BYTES[0] ^ buffer[bPosition - 0x4]) == 0x0
                && (HttpHandshakeUtility.CRLF_BYTES[1] ^ buffer[bPosition - 0x3]) == 0x0
                && (HttpHandshakeUtility.CRLF_BYTES[0] ^ buffer[bPosition - 0x2]) == 0x0
                && (HttpHandshakeUtility.CRLF_BYTES[1] ^ buffer[bPosition - 0x1]) == 0x0)
                {
                    var httpHeader = Encoding.UTF8.GetString(buffer, 0, bPosition - 0x4);

                    // TODO: Decide whether formatting checks should be done here?

                    return httpHeader;
                }
            }
            while (bRead != 0x0);

            throw new InvalidDataException("The data stream timed out without sending a CRLF.");
        }

        public static async Task<HttpHandshake> ReadFromStreamAsync(WebSocketConfig config, Stream connection, CancellationToken token)
        {
            var rawRequest = await GetRawRequestFromStreamAsync(connection, token);

            var headerStrings = rawRequest.Split(new[] { HttpHandshakeUtility.CRLF }, StringSplitOptions.None);

            if (headerStrings.Length == 0)
                throw new InvalidDataException("No valid header strings found in the HTTP request.");

            var httpHeaderMatch = HttpHandshakeUtility.GET_HTTP_HEADER.Match(headerStrings[0]);
            if (!httpHeaderMatch.Success)
                throw new InvalidDataException("The received HTTP request was of an invalid format.");

            var resourceLocation = httpHeaderMatch.Groups[2].Value;
            var protoVersion = httpHeaderMatch.Groups[3].Value;

            var headers = new Dictionary<string, string>();

            for (int i = 1; i < headerStrings.Length; i++)
            {
                var headerFormatMatch = HttpHandshakeUtility.HEADER_MATCH.Match(headerStrings[i]);
                if (!headerFormatMatch.Success)
                    throw new InvalidDataException("The received HTTP request contains a malformed header.");

                headers.Add(headerFormatMatch.Groups[1].Value, headerFormatMatch.Groups[2].Value);
            }

            return new HttpHandshake(HttpMethod.Get, float.Parse(protoVersion), new Uri(string.Concat(config.Host, resourceLocation)), headers);
        }

        #endregion Static Members
    }
}