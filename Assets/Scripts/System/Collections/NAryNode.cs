#region

using System;
using System.Linq;
using Unity.Mathematics;

#endregion

namespace Wyd.System.Collections
{
    public class NAryNode<T> : INodeCollection<T> where T : unmanaged
    {
        private readonly int _CellLength;
        private readonly int _Partitions;

        private NAryNode<T>[] _Nodes;

        public T Value { get; private set; }

        public bool IsUniform => _Nodes == null;

        public NAryNode(int cellLength, int partitions, T value)
        {
            // check if childNodeSize is power of two
            if ((cellLength <= 0) || ((cellLength & (cellLength - 1)) != 0))
            {
                throw new ArgumentException($"Size must be a power of two (is {_CellLength}).", nameof(cellLength));
            }

            _Nodes = null;
            _CellLength = cellLength;
            _Partitions = partitions;
            Value = value;
        }

        private void Collapse()
        {
            if (IsUniform)
            {
                return;
            }

            Value = _Nodes[0].Value;
            _Nodes = null;
        }

        private void Populate()
        {
            bool cellLengthCheck = _CellLength < _Partitions;

            // if _CellLength < _Partitions (i.e. we can't split the current node into _Partitions# of nodes)
            // then we instead use the current _CellLength as our partitions (this will create 1x1x1 nodes)
            if (cellLengthCheck)
            {
                int cubicNodes = (int)math.pow(_CellLength, 3);
                _Nodes = new NAryNode<T>[cubicNodes];

                for (int i = 0; i < _Nodes.Length; i++)
                {
                    _Nodes[i] = new NAryNode<T>(1, 1, Value);
                }
            }
            else
            {
                int cubicNodes = (int)math.pow(_Partitions, 3);
                _Nodes = new NAryNode<T>[cubicNodes];

                int childCellLength = _CellLength / _Partitions;

                for (int i = 0; i < cubicNodes; i++)
                {
                    _Nodes[i] = new NAryNode<T>(childCellLength, childCellLength < _Partitions ? childCellLength : _Partitions, Value);
                }
            }
        }


        public T GetPoint(float3 point)
        {
            if (IsUniform)
            {
                return Value;
            }

            int partitionedSize = _CellLength / _Partitions;

            (int index, float3 indexPoint) = GetPartitionedIndex(point, partitionedSize);

            return _Nodes[index].GetPoint(point - (indexPoint * partitionedSize));
        }

        public void SetPoint(float3 point, T value)
        {
            if (IsUniform && (value.GetHashCode() == Value.GetHashCode()))
            {
                return;
            }

            if (_CellLength == 1)
            {
                Value = value;
                return;
            }

            int partitionedSize = _CellLength / _Partitions;

            if (IsUniform)
            {
                Populate();
            }

            (int index, float3 indexPoint) = GetPartitionedIndex(point, partitionedSize);

            float3 adjustedPoint = point - (indexPoint * partitionedSize);

            _Nodes[index].SetPoint(adjustedPoint, value);

            if (CheckShouldCollapse())
            {
                Collapse();
            }
        }

        private (int, float3) GetPartitionedIndex(float3 point, int partitionedSize)
        {
            float3 indexPoint = math.floor(point / partitionedSize);
            int index = WydMath.PointToIndex(indexPoint, _CellLength < _Partitions ? _CellLength : _Partitions);

            return (index, indexPoint);
        }

        private bool CheckShouldCollapse()
        {
            if (IsUniform)
            {
                return false;
            }

            T firstValue = _Nodes[0].Value;

            // avoiding using linq here for performance sensitivity
            return _Nodes.All(node => node.IsUniform && (node.Value.GetHashCode() == firstValue.GetHashCode()));
        }
    }
}
