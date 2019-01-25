using System;

namespace WsLite.Http
{
    /// <summary>
    /// A thread-local wrapper for <see cref="Random"/> for use in static methods.
    /// </summary>
    internal static class ThreadSafeRandom
    {
        private static readonly Random _global = new Random();

        [ThreadStatic]
        private static Random _local;

        private static Random ThreadLocalRandom
        {
            get
            {
                if (_local == null)
                {
                    lock (_global)
                    {
                        if (_local == null)
                            _local = new Random(_global.Next());
                    }
                }

                return _local;
            }
            set => _local = value;
        }

        public static int Next(int min, int max)
            => ThreadLocalRandom.Next(min, max);

        public static void NextBytes(byte[] buff)
            => ThreadLocalRandom.NextBytes(buff);
    }
}