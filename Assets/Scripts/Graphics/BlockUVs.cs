#region

using Unity.Mathematics;
using UnityEngine;

#endregion

namespace Wyd.Graphics
{
    public class BlockUVs
    {
        public float3 BottomLeft { get; }
        public float3 TopLeft { get; }
        public float3 BottomRight { get; }
        public float3 TopRight { get; }

        public float3 this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return BottomLeft;
                    case 1: return TopLeft;
                    case 2: return BottomRight;
                    case 3: return TopRight;
                    default: return float3.zero;
                }
            }
        }

        public BlockUVs(float3 bottomLeft, float3 topLeft, float3 bottomRight, float3 topRight)
        {
            BottomLeft = bottomLeft;
            TopLeft = topLeft;
            BottomRight = bottomRight;
            TopRight = topRight;
        }

        public override string ToString() =>
            $"[{nameof(BottomLeft)}: {BottomLeft}], [{nameof(TopLeft)}: {TopLeft}], "
            + $"[{nameof(BottomRight)}: {BottomRight}], [{nameof(TopRight)}: {TopRight}]";
    }
}
