namespace WsLite.Socket
{
    internal static class ByteExtensions
    {
        public static bool IsBitSet(this byte source, int position) => (source & (1 << position)) != 0;
    }
}