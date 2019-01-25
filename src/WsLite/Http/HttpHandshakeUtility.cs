using System;
using System.Buffers;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WsLite.Http
{
    /// <summary>
    /// Contains helper methods for operations used during the HTTP-based client/server handshake.
    /// </summary>
    internal static class HttpHandshakeUtility
    {
        /// <summary>
        /// A thread-local instance of the <see cref="SHA1"/> class, used to generate the hashed value returned in Sec-WebSocket-Accept header.
        /// </summary>
        [ThreadStatic]
        private static SHA1 _sha1;

        private static SHA1 SHA1
        {
            get
            {
                if (_sha1 == null)
                    _sha1 = SHA1.Create();
                return _sha1;
            }
        }

        /// <summary>
        /// Matches a string of the format "GET * HTTP/1.1", splitting into group 1: GET, group 2: resource location, group 3: 1.1 (Protocol version).
        /// </summary>
        public static Regex GET_HTTP_HEADER = new Regex(@"^(GET)\s(.*)\sHTTP\/(\d\.\d)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Matches a string of the format "Header-Name: *", splitting into group 1: Header-Name, group 2: value.
        /// </summary>
        public static Regex HEADER_MATCH = new Regex(@"([\w|-]+): ([^\n]+)", RegexOptions.Compiled);

        /// <summary>
        /// A shared <see cref="ArrayPool{T}"/> providing buffers for incoming request data.
        /// </summary>
        public static ArrayPool<byte> ArrayPool => ArrayPool<byte>.Create();

        /// <summary>
        /// CRLF return used to delimit HTTP headers.
        /// </summary>
        public static string CRLF = "\r\n";

        /// <summary>
        /// HTTP header message for 101 Switching protocol.
        /// </summary>
        public static string SWITCHING_PROTOCOLS = "101 Switching Protocol";

        /// <summary>
        /// HTTP header message for 400 bad request.
        /// </summary>
        public static string BAD_REQUEST = "400 Bad Request";

        /// <summary>
        /// Formattable string for any generic HTTP response.
        /// </summary>
        public static string HTTP_RESPONSE_HEADER = "HTTP/1.1 {0}\r\n";

        /// <summary>
        /// Formattable string for any generic HTTP response header.
        /// </summary>
        public static string HTTP_HEADER = "{0}: {1}\r\n";

        /// <summary>
        /// CRLF return used to delimit HTTP headers.
        /// </summary>
        public static byte[] CRLF_BYTES = Encoding.UTF8.GetBytes(CRLF);

        /// <summary>
        /// A "magic value" appended to the Sec-WebSocket-Key provided by a client.
        /// </summary>
        private const string _magicGuid = @"258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        /// <summary>
        /// Generates a random 16-byte sequence for use in the Sec-WebSocket-Key header of a client handshake.
        /// </summary>
        /// <returns></returns>
        public static string GetSecWebSocketKey()
        {
            var keyBuffer = new byte[16];
            ThreadSafeRandom.NextBytes(keyBuffer);

            return Convert.ToBase64String(keyBuffer);
        }

        /// <summary>
        /// Computes the Sec-WebSocket-Accept header field value for a server response to a client handshake.
        /// </summary>
        /// <param name="secWebSocketKey">The client-provided Sec-WebSocket-Key.</param>
        /// <returns></returns>
        public static string GetSecWebSocketAcceptString(string secWebSocketKey)
        {
            var concatKey = string.Concat(secWebSocketKey, _magicGuid);
            var shaHash = SHA1.ComputeHash(Encoding.UTF8.GetBytes(concatKey));

            return Convert.ToBase64String(shaHash);
        }

        /// <summary>
        /// Generates an HTTP response message.
        /// </summary>
        /// <param name="code">The response code.</param>
        /// <param name="headers">Headers to be sent along with the response code.</param>
        /// <returns></returns>
        public static byte[] GenerateResponseMessage(int code, Dictionary<string, string> headers = null)
        {
            string codeString;
            switch (code)
            {
                case 101:
                    codeString = SWITCHING_PROTOCOLS;
                    break;

                case 400:
                    codeString = BAD_REQUEST;
                    break;

                default:
                    throw new ArgumentException("Passed an unsupported response code to the message generator.");
            }

            var sb = new StringBuilder();
            sb.Append(string.Format(HTTP_RESPONSE_HEADER, codeString));

            if (headers != null)
                foreach (var header in headers)
                    sb.Append(string.Format(HTTP_HEADER, header.Key, header.Value));

            sb.Append(CRLF);

            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}