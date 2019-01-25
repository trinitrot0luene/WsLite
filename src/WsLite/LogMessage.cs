using System;

namespace WsLite
{
    public class LogMessage
    {
        public string Source { get; }

        public string Message { get; }

        public Exception Exception { get; }

        public LogMessage(string source, string message, Exception exception = null)
        {
            Source = source;
            Message = message;
            Exception = exception;
        }
    }
}