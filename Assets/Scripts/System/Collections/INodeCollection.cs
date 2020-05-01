#region

using System.Collections.Generic;
using Unity.Mathematics;

#endregion

namespace Wyd.System.Collections
{
    public interface INodeCollection<T>
    {
        T Value { get; }
        bool IsUniform { get; }

        T GetPoint(float3 point);
        void SetPoint(float3 point, T value);
        IEnumerable<T> GetAllData();
    }
}
