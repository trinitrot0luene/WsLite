using System;
using System.Collections.Generic;
using System.Text;

namespace WsLite.Handlers
{
    public class HandlerContext
    {
        public WsLiteServer Server { get; }

        public WsLiteConnection Connection { get; }

        public IServiceProvider Services { get; }

        public HandlerContext(WsLiteServer server, WsLiteConnection connection, IServiceProvider services)
        {
            Server = server;
            Connection = connection;
            Services = services;
        }
    }
}
