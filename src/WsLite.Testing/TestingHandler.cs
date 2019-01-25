using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using WsLite.Handlers;

namespace WsLite.Testing
{
    public sealed class TestingHandler : HandlerBase<HandlerContext>
    {
        protected override Task OnTextMessageAsync(ArraySegment<byte> buffer)
        {
            Console.WriteLine(Encoding.UTF8.GetString(buffer.Array, buffer.Offset, buffer.Count));

            return Task.CompletedTask;
        }
    }
}
