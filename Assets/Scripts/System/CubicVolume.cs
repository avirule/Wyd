#region

using Unity.Mathematics;

#endregion

// ReSharper disable ConvertToAutoPropertyWhenPossible

namespace Wyd.System
{
    public class CubicVolume
    {
        private readonly float3 _CenterPoint;
        private readonly float _Size;

        public float3 CenterPoint => _CenterPoint;
        public float Size => _Size;

        public float Extent => _Size / 2f;
        public float3 MinPoint => _CenterPoint - Extent;
        public float3 MaxPoint => _CenterPoint + Extent;

        public CubicVolume(float3 centerPoint, float size) => (_CenterPoint, _Size) = (centerPoint, size);

        public bool Contains(float3 point) => math.all(point > MinPoint) && math.all(point < MaxPoint);
        public bool ContainsMinBiased(float3 point) => math.all(point >= MinPoint) && math.all(point < MaxPoint);
        public bool ContainsMaxBiased(float3 point) => math.all(point > MinPoint) && math.all(point <= MaxPoint);

        public override string ToString() => $"(center: {CenterPoint}, size: {Size}, min: {MinPoint}, max: {MaxPoint})";
    }
}
