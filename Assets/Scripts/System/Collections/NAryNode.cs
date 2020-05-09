// #region
//
// using System;
// using System.Collections.Generic;
// using System.Linq;
// using Unity.Mathematics;
//
// // ReSharper disable ConvertToAutoProperty
// // ReSharper disable ConvertToAutoPropertyWhenPossible
// // ReSharper disable ConvertToAutoPropertyWithPrivateSetter
//
// #endregion
//
// namespace Wyd.System.Collections
// {
//     public class NAryNode<T> where T : IEquatable<T>
//     {
//         private NAryNode<T>[] _Nodes;
//         private bool _IsUniform;
//         private T _Value;
//
//         public T Value => _Value;
//         public bool IsUniform => _IsUniform;
//
//         public NAryNode<T> this[int index] => _Nodes[index];
//
//         public NAryNode(T value)
//         {
//             _Nodes = null;
//             _Value = value;
//         }
//
//         private void Collapse()
//         {
//             if (_IsUniform)
//             {
//                 return;
//             }
//
//             _IsUniform = true;
//             _Value = _Nodes[0]._Value;
//             _Nodes = null;
//         }
//
//         private void Populate()
//         {
//             bool cellLengthCheck = _CellLength < _Partitions;
//
//             // if _CellLength < _Partitions (i.e. we can't split the current node into _Partitions# of nodes)
//             // then we instead use the current _CellLength as our partitions (this will create 1x1x1 nodes)
//             if (cellLengthCheck)
//             {
//                 int cubicNodes = (int)math.pow(_CellLength, 3);
//                 _Nodes = new NAryNode<T>[cubicNodes];
//
//                 for (int i = 0; i < _Nodes.Length; i++)
//                 {
//                     _Nodes[i] = new NAryNode<T>(1, 1, _Value);
//                 }
//             }
//             else
//             {
//                 int cubicNodes = (int)math.pow(_Partitions, 3);
//                 _Nodes = new NAryNode<T>[cubicNodes];
//
//                 int childCellLength = _CellLength / _Partitions;
//
//                 for (int i = 0; i < cubicNodes; i++)
//                 {
//                     _Nodes[i] = new NAryNode<T>(childCellLength, childCellLength < _Partitions ? childCellLength : _Partitions, _Value);
//                 }
//             }
//         }
//
//         public void SetPoint(float3 point, T value)
//         {
//             if (_IsUniform && (value.GetHashCode() == _Value.GetHashCode()))
//             {
//                 return;
//             }
//
//             if (_CellLength == 1)
//             {
//                 _Value = value;
//                 return;
//             }
//
//             int partitionedSize = _CellLength / _Partitions;
//
//             if (_IsUniform)
//             {
//                 Populate();
//             }
//
//             (int index, float3 indexPoint) = GetPartitionedIndex(point, partitionedSize);
//
//             float3 adjustedPoint = point - (indexPoint * partitionedSize);
//
//             _Nodes[index].SetPoint(adjustedPoint, value);
//
//             if (CheckShouldCollapse())
//             {
//                 Collapse();
//             }
//         }
//
//         public IEnumerable<T> GetAllData() => throw new NotImplementedException();
//
//         private (int, float3) GetPartitionedIndex(float3 point, int partitionedSize)
//         {
//             float3 indexPoint = math.floor(point / partitionedSize);
//             int index = WydMath.ProjectToIndex(indexPoint, _CellLength < _Partitions ? _CellLength : _Partitions);
//
//             return (index, indexPoint);
//         }
//
//         private bool CheckShouldCollapse()
//         {
//             if (_IsUniform)
//             {
//                 return false;
//             }
//
//             T firstValue = _Nodes[0]._Value;
//
//             // avoiding using linq here for performance sensitivity
//             return _Nodes.All(node => node._IsUniform && node._Value.Equals(firstValue));
//         }
//     }
// }
