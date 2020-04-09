#region

using Unity.Mathematics;

#endregion

namespace Wyd.System
{
    public struct Volume
    {
        public float3 CenterPoint { get; }
        public float3 MinPoint { get; }
        public float3 MaxPoint { get; }
        public float3 Extents { get; }
        public float3 Size { get; }

        public Volume(float3 centerPoint, float3 size)
        {
            CenterPoint = centerPoint;
            Size = size;
            Extents = Size / 2f;
            MinPoint = CenterPoint - Extents;
            MaxPoint = CenterPoint + Extents;
        }

        public bool Contains(float3 point) => math.all(point > MinPoint) && math.all(point < MaxPoint);
        public bool ContainsMinBiased(float3 point) => math.all(point >= MinPoint) && math.all(point < MaxPoint);
        public bool ContainsMaxBiased(float3 point) => math.all(point > MinPoint) && math.all(point <= MaxPoint);

        public override string ToString() => $"(center: {CenterPoint}, size: {Size}, min: {MinPoint}, max: {MaxPoint})";
    }
}
