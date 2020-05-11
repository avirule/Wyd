#region

using Unity.Mathematics;

#endregion

namespace Wyd
{
    public class Volume
    {
        public float3 CenterPoint { get; }
        public float3 Extents { get; }

        public float3 MinPoint => CenterPoint - Extents;
        public float3 MaxPoint => CenterPoint + Extents;
        public float3 Size => Extents * 2f;

        public Volume(float3 centerPoint, float3 size)
        {
            CenterPoint = centerPoint;
            Extents = size / 2f;
        }

        public bool Contains(float3 point) => math.all(point > MinPoint) && math.all(point < MaxPoint);
        public bool ContainsMinBiased(float3 point) => math.all(point >= MinPoint) && math.all(point < MaxPoint);
        public bool ContainsMaxBiased(float3 point) => math.all(point > MinPoint) && math.all(point <= MaxPoint);

        public override string ToString() => $"(center: {CenterPoint}, size: {Size}, min: {MinPoint}, max: {MaxPoint})";
    }
}
