namespace WsLite.Socket
{
    public enum OpCode
    {
        /// <summary>
        /// Indicates that this frame has the same type as preceding frames.
        /// </summary>
        Continuation = 0x0,

        /// <summary>
        /// Indicates that this frame contains text data.
        /// </summary>
        Text = 0x1,

        /// <summary>
        /// Indicates that this frame contains binary data.
        /// </summary>
        Binary = 0x2,

        /// <summary>
        /// Indicates that this frame is a close control frame.
        /// </summary>
        Close = 0x8,

        /// <summary>
        /// Indicates that this frame is a ping control frame.
        /// </summary>
        Ping = 0x9,

        /// <summary>
        /// Indicates that this frame is a pong control frame.
        /// </summary>
        Pong = 0xA
    }
}