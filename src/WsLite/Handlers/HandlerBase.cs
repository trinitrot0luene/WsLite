using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WsLite.Socket;

namespace WsLite.Handlers
{
    public abstract class HandlerBase<TContext> where TContext : HandlerContext
    {
        private readonly SemaphoreSlim _closeExchange = new SemaphoreSlim(1, 1);

        private bool pingingClient = false;

        private bool connectionClosing = false;

        protected TContext Context { get; private set; }

        internal void SetContext(TContext context) => Context = context;

        internal async Task HandleMessageAsync(OpCode opCode, WebSocketCloseStatus? status, ArraySegment<byte> buffer)
        {
            await BeforeExecuteAsync(opCode, status, buffer);

            switch (opCode)
            {
                case OpCode.Text:
                    await OnTextMessageAsync(buffer);
                    break;
                case OpCode.Binary:
                    await OnBinaryMessageAsync(buffer);
                    break;
                case OpCode.Close:
                    await OnCloseFrameAsync(status);
                    break;
                case OpCode.Ping:
                    await OnPingFrameAsync();
                    break;
                case OpCode.Pong:
                    await OnPongFrameAsync();
                    break;
            }

            await AfterExecuteAsync(opCode, status, buffer);
        }

        internal virtual Task HandleLogAsync(LogMessage message) => Task.CompletedTask;

        internal virtual Task HandleErrorAsync(Exception ex) => Task.CompletedTask;

        protected virtual Task BeforeExecuteAsync(OpCode opCode, WebSocketCloseStatus? status, ArraySegment<byte> buffer) => Task.CompletedTask;

        protected virtual Task AfterExecuteAsync(OpCode opCode, WebSocketCloseStatus? status, ArraySegment<byte> buffer) => Task.CompletedTask;

        protected virtual Task OnTextMessageAsync(ArraySegment<byte> buffer) => Task.CompletedTask;

        protected virtual Task OnBinaryMessageAsync(ArraySegment<byte> buffer) => Task.CompletedTask;

        protected async Task OnCloseFrameAsync(WebSocketCloseStatus? closeStatus)
        {
            if (!connectionClosing)
            {
                await Context.Connection.CloseAsync(closeStatus ?? 0);
                connectionClosing = true;
            }
            else
            {
                Context.Connection.OnMessage -= HandleMessageAsync;
                Context.Connection.OnLog -= HandleLogAsync;
                Context.Connection.OnError -= HandleErrorAsync;

                Context.Server.Disconnect(Context.Connection);
            }
        }

        protected Task OnPingFrameAsync()
            => Context.Connection.SendAsync(OpCode.Pong);

        protected Task OnPongFrameAsync()
        {
            if (!pingingClient)
                return Context.Connection.CloseAsync(WebSocketCloseStatus.ProtocolError);
            else
            {
                pingingClient = false;
                return Task.CompletedTask;
            }
        }
    }
}
