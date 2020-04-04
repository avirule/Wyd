#region

using UnityEngine;

#endregion

namespace Wyd.Graphics
{
    public class BlockUVs
    {
        public Vector3 BottomLeft { get; }
        public Vector3 TopLeft { get; }
        public Vector3 BottomRight { get; }
        public Vector3 TopRight { get; }

        public Vector3 this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return BottomLeft;
                    case 1: return TopLeft;
                    case 2: return BottomRight;
                    case 3: return TopRight;
                    default: return Vector3.zero;
                }
            }
        }

        public BlockUVs(Vector3 bottomLeft, Vector3 topLeft, Vector3 bottomRight, Vector3 topRight)
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
