#region

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

#endregion

namespace Wyd.System.Collections
{
    public class OctreeNode
    {
        private static readonly float3[] _coordinates =
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


        private readonly List<OctreeNode> _Nodes;
        private readonly Volume _Volume;

        public Volume Volume => _Volume;
        public bool IsUniform => _Nodes.Count == 0;

        public ushort Value { get; private set; }

        public OctreeNode(float3 centerPoint, float size, ushort value)
        {
            _Nodes = new List<OctreeNode>();

            _Volume = new Volume(centerPoint, new float3(size));
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
            float3 offset = new float3(_Volume.Extents / 2f);

            _Nodes.Clear();

            _Nodes.Add(new OctreeNode(_Volume.CenterPoint + (_coordinates[0] * offset), _Volume.Extents.x, Value));
            _Nodes.Add(new OctreeNode(_Volume.CenterPoint + (_coordinates[1] * offset), _Volume.Extents.x, Value));
            _Nodes.Add(new OctreeNode(_Volume.CenterPoint + (_coordinates[2] * offset), _Volume.Extents.x, Value));
            _Nodes.Add(new OctreeNode(_Volume.CenterPoint + (_coordinates[3] * offset), _Volume.Extents.x, Value));
            _Nodes.Add(new OctreeNode(_Volume.CenterPoint + (_coordinates[4] * offset), _Volume.Extents.x, Value));
            _Nodes.Add(new OctreeNode(_Volume.CenterPoint + (_coordinates[5] * offset), _Volume.Extents.x, Value));
            _Nodes.Add(new OctreeNode(_Volume.CenterPoint + (_coordinates[6] * offset), _Volume.Extents.x, Value));
            _Nodes.Add(new OctreeNode(_Volume.CenterPoint + (_coordinates[7] * offset), _Volume.Extents.x, Value));
        }

        public ushort GetPoint(float3 point)
        {
            point = math.floor(point);

            if (!ContainsMinBiased(point))
            {
                throw new ArgumentOutOfRangeException(
                    $"Attempted to get point {point} in {nameof(OctreeNode)}, but {nameof(_Volume)} does not contain it.\r\n"
                    + $"State Information: [Volume {_Volume}], [{nameof(IsUniform)} {IsUniform}], [Branches {_Nodes.Count}], "
                    + (_Nodes.Count > 0
                        ? $"[Branch Values {string.Join(", ", _Nodes.Select(node => node.Value))}"
                        : string.Empty));
            }


            if (!IsUniform)
            {
                int octant = DetermineOctant(point);

                if (octant >= _Nodes.Count)
                {
                    throw new ArgumentOutOfRangeException(
                        $"Attempted to step into octant of {nameof(OctreeNode)} and failed ({nameof(GetPoint)}).\r\n"
                        + $"State Information: [Volume {_Volume}], [{nameof(IsUniform)} {IsUniform}], [Branches {_Nodes.Count}], "
                        + (_Nodes.Count > 0
                            ? $"[Branch Values {string.Join(", ", _Nodes.Select(node => node.Value))}]"
                            : string.Empty)
                        + $"[Octant {octant}]");
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

        public bool TryGetPoint(float3 point, out ushort value)
        {
            value = 0;

            if (!ContainsMinBiased(point))
            {
                return false;
            }
            else if (!IsUniform)
            {
                int octant = DetermineOctant(point);
                return _Nodes[octant].TryGetPoint(point, out value);
            }
            else
            {
                value = Value;
                return true;
            }
        }

        public ushort UncheckedGetPoint(float3 point)
        {
            point = math.floor(point);

            if (!IsUniform)
            {
                int octant = DetermineOctant(point);
                return _Nodes[octant].UncheckedGetPoint(point);
            }
            else
            {
                return Value;
            }
        }

        public void SetPoint(float3 point, ushort newValue)
        {
            point = math.floor(point);

            if (!ContainsMinBiased(point))
            {
                throw new ArgumentOutOfRangeException(
                    $"Attempted to set point {point} in {nameof(OctreeNode)}, but {nameof(_Volume)} does not contain it.\r\n"
                    + $"State Information: [Volume {_Volume}], [{nameof(IsUniform)} {IsUniform}], [Branches {_Nodes.Count}], "
                    + (_Nodes.Count > 0
                        ? $"[Branch Values {string.Join(", ", _Nodes.Select(node => node.Value))}"
                        : string.Empty));
            }

            if (IsUniform && Value.Equals(newValue))
            {
                // operation does nothing, so return
                return;
            }

            if (_Volume.Size.x <= 1f)
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
                throw new ArgumentOutOfRangeException(
                    $"Attempted to step into octant of {nameof(OctreeNode)} and failed ({nameof(SetPoint)}).\r\n"
                    + $"State Information: [Volume {_Volume}], [{nameof(IsUniform)} {IsUniform}], [Branches {_Nodes.Count}], "
                    + (_Nodes.Count > 0
                        ? $"[Branch Values {string.Join(", ", _Nodes.Select(node => node.Value))}]"
                        : string.Empty)
                    + $"[Octant {octant}]");
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

        public void UncheckedSetPoint(float3 point, ushort newValue)
        {
            point = math.floor(point);

            if (IsUniform && Value.Equals(newValue))
            {
                // operation does nothing, so return
                return;
            }

            if (_Volume.Size.x <= 1f)
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

            // recursively dig into octree and set
            _Nodes[octant].UncheckedSetPoint(point, newValue);

            // on each recursion back-step, ensure integrity of node
            // and collapse if all child node values are equal
            if (!IsUniform && CheckShouldCollapse())
            {
                Collapse();
            }
        }

        private bool CheckShouldCollapse()
        {
            if (IsUniform)
            {
                throw new ArgumentOutOfRangeException(
                    $"Attempted to check for required collapsing of {nameof(OctreeNode)} and failed..\r\n"
                    + $"State Information: [Volume {_Volume}], [{nameof(IsUniform)} {IsUniform}], [Branches {_Nodes.Count}], "
                    + (_Nodes.Count > 0
                        ? $"[Branch Values {string.Join(", ", _Nodes.Select(node => node.Value))}]"
                        : string.Empty));
            }

            ushort firstValue = _Nodes[0].Value;

            // avoiding using linq here for performance sensitivity
            for (int index = 0; index < 8 /* octants! */; index++)
            {
                OctreeNode node = _Nodes[index];

                if (!node.IsUniform || (node.Value.GetHashCode() != firstValue.GetHashCode()))
                {
                    return false;
                }
            }

            return true;
        }

        public IEnumerable<ushort> GetAllData()
        {
            int3 sizeInt = WydMath.ToInt(_Volume.Size);

            for (int index = 0; index < WydMath.Product(_Volume.Size); index++)
            {
                yield return GetPoint(_Volume.MinPoint + WydMath.IndexTo3D(index, sizeInt));
            }
        }

        public IEnumerable<ushort> UncheckedGetAllData()
        {
            int3 sizeInt = WydMath.ToInt(_Volume.Size);

            for (int index = 0; index < WydMath.Product(_Volume.Size); index++)
            {
                yield return UncheckedGetPoint(_Volume.MinPoint + WydMath.IndexTo3D(index, sizeInt));
            }
        }

        #region HELPER METHODS

        public bool ContainsMinBiased(float3 point) =>
            Volume.ContainsMinBiased(point);

        // indexes:
        // bottom half quadrant:
        // 1 3
        // 0 2
        // top half quadrant:
        // 5 7
        // 4 6
        private int DetermineOctant(float3 point) =>
            (point.x < _Volume.CenterPoint.x ? 0 : 1)
            + (point.y < _Volume.CenterPoint.y ? 0 : 4)
            + (point.z < _Volume.CenterPoint.z ? 0 : 2);

        #endregion
    }
}
