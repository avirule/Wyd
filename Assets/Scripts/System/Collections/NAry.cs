// using System;
// using System.Collections.Generic;
// using System.Linq.Expressions;
// using Unity.Mathematics;
//
// namespace Wyd.System.Collections
// {
//     public class NAry<T> : INodeCollection<T> where T: unmanaged, IEquatable<T>
//     {
//         private readonly NAryNode<T> _RootNode;
//         private readonly int _PartitionLength;
//
//         public int Partitions { get; }
//         public int Length { get; }
//
//         public T Value { get; }
//         public bool IsUniform { get; }
//
//         public NAry(int partitions, int length, T value)
//         {
//             _RootNode = new NAryNode<T>();
//             Partitions = partitions;
//             Length = length;
//
//             // todo check that
//             _PartitionLength = Length / partitions;
//         }
//
//         public T GetPoint(float3 point)
//         {
//             NAryNode<T> currentNode = _RootNode;
//             bool maximumDepthReached = false;
//
//             for (int localPartitionLength = _PartitionLength;
//                 !currentNode.IsUniform;
//                 localPartitionLength /= Partitions, maximumDepthReached = localPartitionLength <= Partitions)
//             {
//                 float3 pointIndexAligned = maximumDepthReached ? point : math.floor(point / Partitions);
//                 int index = WydMath.PointToIndex(pointIndexAligned, localPartitionLength);
//
//                 currentNode = currentNode[index];
//                 point -= pointIndexAligned *
//             }
//
//             return currentNode.Value;
//         }
//
//         public void SetPoint(float3 point, T value)
//         {
//             throw new NotImplementedException();
//         }
//
//         public IEnumerable<T> GetAllData() => throw new NotImplementedException();
//     }
// }


