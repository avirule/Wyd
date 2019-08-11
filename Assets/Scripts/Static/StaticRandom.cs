#region

using System;

#endregion

namespace Static
{
    public static class StaticRandom
    {
        static StaticRandom()
        {
            Random = new Random();
        }

        private static Random Random { get; }

        public static int Next(int min, int max)
        {
            return Random.Next(min, max + 1);
        }

        public static byte[] NextBytes(ushort length)
        {
            byte[] buffer = new byte[length];

            Random.NextBytes(buffer);

            return buffer;
        }
    }
}