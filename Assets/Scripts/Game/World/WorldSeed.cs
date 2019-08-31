#region

using System;
using System.Text;
using UnityEngine;
using Random = System.Random;

#endregion

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace Game.World
{
    public class WorldSeed
    {
        private readonly string _BaseSeed;
        private readonly byte[] _SeedBytes;

        public readonly float Normalized;
        public readonly int SeedValue;

        public WorldSeed(string baseSeed)
        {
            _BaseSeed = baseSeed;

            byte[] baseSeedBytes = Encoding.UTF8.GetBytes(_BaseSeed);
            ushort randomSeedBytes = BitConverter.ToUInt16(baseSeedBytes, 0);

            _SeedBytes = new byte[randomSeedBytes];
            new Random(randomSeedBytes).NextBytes(_SeedBytes);

            SeedValue = BitConverter.ToInt32(_SeedBytes, 0);
            Normalized = Mathf.InverseLerp(int.MinValue, int.MaxValue, SeedValue);
        }

        public static implicit operator int(WorldSeed seed)
        {
            return seed.SeedValue;
        }
    }
}