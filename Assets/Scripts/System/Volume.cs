#region

using Unity.Mathematics;

#endregion

namespace Wyd.System
{
    public struct Volume
    {
        public float3 CenterPoint { get; private set; }
        public float3 MinPoint { get; private set; }
        public float3 MaxPoint { get; private set; }
        public float3 Extents { get; private set; }
        public float3 Size { get; private set; }

        public Volume(float3 centerPoint, float3 size)
        {
            CenterPoint = centerPoint;
            Size = size;
            Extents = Size / 2f;
            MinPoint = CenterPoint - Extents;
            MaxPoint = CenterPoint + Extents;
        }

        public void SetMinMaxPoints(float3 minPoint, float3 maxPoint)
        {
            MinPoint = minPoint;
            MaxPoint = maxPoint;
            Size = MaxPoint - MinPoint;
            Extents = Size / 2f;
            CenterPoint = MinPoint + Extents;
        }

        public bool Contains(float3 point) => math.all(point < MaxPoint) && math.all(point > MinPoint);

        public override string ToString() => $"(center: {CenterPoint}, size: {Size}, min: {MinPoint}, max: {MaxPoint})";
    }
}
