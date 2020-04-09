#region

using System.Collections.Generic;
using System.Linq;
using Serilog;
using Unity.Mathematics;

#endregion

namespace Wyd.System.Collections
{
    public class OctreeNode<T> where T : unmanaged
    {
        private static readonly float3[] _Coordinates =
        {
            new float3(-1, -1, -1),
            new float3(1, -1, -1),
            new float3(-1, -1, 1),
            new float3(1, -1, 1),

            new float3(-1, 1, -1),
            new float3(1, 1, -1),
            new float3(-1, 1, 1),
            new float3(1, 1, 1)
        };


        private readonly List<OctreeNode<T>> _Nodes;

        public Volume Volume { get; }
        public T Value { get; private set; }
        public bool IsUniform => _Nodes.Count == 0;

        public OctreeNode(float3 centerPoint, float size, T value)
        {
            _Nodes = new List<OctreeNode<T>>();

            Volume = new Volume(centerPoint, new float3(size));
            Value = value;
        }

        public void Collapse()
        {
            if (IsUniform)
            {
                return;
            }

            Value = _Nodes[0].Value;
            _Nodes.Clear();
        }

        public void Populate()
        {
            float centerOffsetValue = Volume.Extents.x / 2f;
            float3 centerOffset = new float3(centerOffsetValue);

            _Nodes.Clear();

            _Nodes.Add(new OctreeNode<T>(Volume.CenterPoint + (_Coordinates[0] * centerOffset),
                Volume.Extents.x, Value));

            _Nodes.Add(new OctreeNode<T>(Volume.CenterPoint + (_Coordinates[1] * centerOffset),
                Volume.Extents.x, Value));

            _Nodes.Add(new OctreeNode<T>(Volume.CenterPoint + (_Coordinates[2] * centerOffset),
                Volume.Extents.x, Value));

            _Nodes.Add(new OctreeNode<T>(Volume.CenterPoint + (_Coordinates[3] * centerOffset),
                Volume.Extents.x, Value));

            _Nodes.Add(new OctreeNode<T>(Volume.CenterPoint + (_Coordinates[4] * centerOffset),
                Volume.Extents.x, Value));

            _Nodes.Add(new OctreeNode<T>(Volume.CenterPoint + (_Coordinates[5] * centerOffset),
                Volume.Extents.x, Value));

            _Nodes.Add(new OctreeNode<T>(Volume.CenterPoint + (_Coordinates[6] * centerOffset),
                Volume.Extents.x, Value));

            _Nodes.Add(new OctreeNode<T>(Volume.CenterPoint + (_Coordinates[7] * centerOffset),
                Volume.Extents.x, Value));
        }

        public T GetPoint(float3 point)
        {
            point = math.floor(point);

            if (!ContainsMinBiased(point))
            {
                Log.Error(
                    $"Attempted to get point {point} in {nameof(OctreeNode<ushort>)}, but {nameof(Volume)} does not contain it.\r\n"
                    + $"State Information: [Volume {Volume}], [{nameof(IsUniform)} {IsUniform}], [Branches {_Nodes.Count}], "
                    + (_Nodes.Count > 0
                        ? $"[Branch Values {string.Join(", ", _Nodes.Select(node => node.Value))}"
                        : string.Empty));
                return default;
            }


            if (!IsUniform)
            {
                int octant = DetermineOctant(point);

                if (octant >= _Nodes.Count)
                {
                    Log.Error(
                        $"Attempted to step into octant of {nameof(OctreeNode<ushort>)} and failed ({nameof(GetPoint)}).\r\n"
                        + $"State Information: [Volume {Volume}], [{nameof(IsUniform)} {IsUniform}], [Branches {_Nodes.Count}], "
                        + (_Nodes.Count > 0
                            ? $"[Branch Values {string.Join(", ", _Nodes.Select(node => node.Value))}]"
                            : string.Empty)
                        + $"[Octant {octant}]");
                    return default;
                }
                else
                {
                    return _Nodes[octant].GetPoint(point);
                }
            }
            else
            {
                return Value;
            }
        }

        public void SetPoint(float3 point, T newValue)
        {
            point = math.floor(point);

            if (!ContainsMinBiased(point))
            {
                Log.Error(
                    $"Attempted to set point {point} in {nameof(OctreeNode<ushort>)}, but {nameof(Volume)} does not contain it.\r\n"
                    + $"State Information: [Volume {Volume}], [{nameof(IsUniform)} {IsUniform}], [Branches {_Nodes.Count}], "
                    + (_Nodes.Count > 0
                        ? $"[Branch Values {string.Join(", ", _Nodes.Select(node => node.Value))}"
                        : string.Empty));
                return;
            }

            if (IsUniform && Value.Equals(newValue))
            {
                // operation does nothing, so return
                return;
            }

            if (Volume.Size.x <= 1f)
            {
                // reached smallest possible depth (usually 1x1x1) so
                // set value and return
                Value = newValue;
                return;
            }

            if (IsUniform)
            {
                // node has no child nodes to traverse, so populate
                Populate();
            }

            int octant = DetermineOctant(point);

            if (octant >= _Nodes.Count)
            {
                Log.Error(
                    $"Attempted to step into octant of {nameof(OctreeNode<ushort>)} and failed ({nameof(SetPoint)}).\r\n"
                    + $"State Information: [Volume {Volume}], [{nameof(IsUniform)} {IsUniform}], [Branches {_Nodes.Count}], "
                    + (_Nodes.Count > 0
                        ? $"[Branch Values {string.Join(", ", _Nodes.Select(node => node.Value))}]"
                        : string.Empty)
                    + $"[Octant {octant}]");
                return;
            }

            // recursively dig into octree and set
            _Nodes[octant].SetPoint(point, newValue);

            // on each recursion back-step, ensure integrity of node
            // and collapse if all child node values are equal
            if (!IsUniform && CheckShouldCollapse())
            {
                Collapse();
            }
        }

        private bool CheckShouldCollapse()
        {
            T firstValue = _Nodes[0].Value;

            // avoiding using linq here for performance sensitivity
            for (int index = 0; index < 8 /* octants! */; index++)
            {
                OctreeNode<T> node = _Nodes[index];

                if (!node.IsUniform || (node.Value.GetHashCode() != firstValue.GetHashCode()))
                {
                    return false;
                }
            }

            return true;
        }

        public IEnumerable<T> GetAllData()
        {
            for (int index = 0; index < WydMath.Product(Volume.Size); index++)
            {
                yield return GetPoint(Volume.MinPoint + WydMath.IndexTo3D(index, WydMath.ToInt(Volume.Size)));
            }
        }


        #region HELPER METHODS

        public bool ContainsMinBiased(float3 point) =>
            (point.x < Volume.MaxPoint.x)
            && (point.y < Volume.MaxPoint.y)
            && (point.z < Volume.MaxPoint.z)
            && (point.x >= Volume.MinPoint.x)
            && (point.y >= Volume.MinPoint.y)
            && (point.z >= Volume.MinPoint.z);

        // indexes:
        // bottom half quadrant:
        // 1 3
        // 0 2
        // top half quadrant:
        // 5 7
        // 4 6
        private int DetermineOctant(float3 point) =>
            (point.x < Volume.CenterPoint.x ? 0 : 1)
            + (point.y < Volume.CenterPoint.y ? 0 : 4)
            + (point.z < Volume.CenterPoint.z ? 0 : 2);

        #endregion
    }
}
