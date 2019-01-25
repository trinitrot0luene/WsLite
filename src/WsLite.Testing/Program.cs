using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace WsLite.Testing
{
    internal static class Program
    {
        static Task Main(string[] args) => new WebSocketServerManager().StartAsync();
    }
}
