using Unity.Mathematics;

namespace Wyd.System.Collections
{
    public interface INodeCollection<T>
    {
        T Value { get; }
        bool IsUniform { get; }

        T GetPoint(float3 point);
        void SetPoint(float3 point, T value);
    }
}
