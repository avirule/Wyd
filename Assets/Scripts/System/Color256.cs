#region

using Unity.Mathematics;
using UnityEngine;

#endregion

namespace Wyd.System
{
    public struct Color256
    {
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public Color256(byte r, byte g, byte b) =>
            (R, G, B) = (r, g, b);

        public static explicit operator Color(Color256 a) =>
            new Color(math.lerp(byte.MinValue, byte.MaxValue, a.R), math.lerp(byte.MinValue, byte.MaxValue, a.G),
                math.lerp(byte.MinValue, byte.MaxValue, a.B), 1f);
    }
}
