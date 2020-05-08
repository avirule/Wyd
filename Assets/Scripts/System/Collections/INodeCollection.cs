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
        int Length { get; }

        T GetPoint(int3 point);
        void SetPoint(int3 point, T value);

        IEnumerable<T> GetAllData();
        void CopyTo(T[] destinationArray);
    }
}
