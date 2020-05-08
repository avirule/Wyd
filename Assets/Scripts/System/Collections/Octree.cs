#region

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

#endregion

namespace Wyd.System.Collections
{
    public class Octree : INodeCollection<ushort>
    {
        private readonly byte _Size;
        private readonly OctreeNode _RootNode;

        public ushort Value => _RootNode.Value;
        public bool IsUniform => _RootNode.IsUniform;

        public int Length { get; }

        public Octree(byte size, ushort initialValue, bool fullyPopulate)
        {
            if ((size <= 0) || ((size & (size - 1)) != 0))
            {
                throw new ArgumentException("Size must be a power of two.", nameof(size));
            }

            _Size = size;
            _RootNode = new OctreeNode(initialValue);

            Length = (int)math.pow(_Size, 3);

            if (fullyPopulate)
            {
                _RootNode.PopulateRecursive(_Size / 2f);
            }
        }


        #region GetPoint

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ushort GetPoint(float3 point) => GetPointIterative(point);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ushort GetPointRecursive(float3 point) => _RootNode.GetPoint(_Size / 2f, point.x, point.y, point.z);

        private ushort GetPointIterative(float3 point)
        {
            OctreeNode currentNode = _RootNode;
            float x = point.x, y = point.y, z = point.z;

            for (float extent = _Size / 2f; !currentNode.IsUniform; extent /= 2f)
            {
                DetermineOctant(extent, ref x, ref y, ref z, out int octant);

                currentNode = currentNode[octant];
            }

            return currentNode.Value;
        }

        #endregion


        #region SetPoint

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPoint(float3 point, ushort value) => SetPointRecursive(point, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetPointNoCollapse(float3 point, ushort value) => SetPointIterative(point, value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetPointRecursive(float3 point, ushort value) => _RootNode.SetPoint(_Size / 2f, point.x, point.y, point.z, value);

        private void SetPointIterative(float3 point, ushort value)
        {
            OctreeNode currentNode = _RootNode;
            float x = point.x, y = point.y, z = point.z;

            for (float extent = _Size / 2f;; extent /= 2f)
            {
                if (currentNode.IsUniform)
                {
                    if (currentNode.Value == value)
                    {
                        return;
                    }
                    else if (extent < 1f)
                    {
                        // reached smallest possible depth (usually 1x1x1) so
                        // set value and return
                        currentNode.Value = value;
                        return;
                    }
                    else
                    {
                        currentNode.Populate();
                    }
                }

                DetermineOctant(extent, ref x, ref y, ref z, out int octant);

                // recursively dig into octree and set
                currentNode = currentNode[octant];
            }
        }

        #endregion


        public IEnumerable<ushort> GetAllData()
        {
            for (int index = 0; index < Length; index++)
            {
                yield return GetPoint(WydMath.IndexTo3D(index, _Size));
            }
        }

        public void CopyTo(ushort[] destinationArray)
        {
            if (destinationArray == null)
            {
                throw new NullReferenceException(nameof(destinationArray));
            }
            else if (destinationArray.Rank != 1)
            {
                throw new RankException("Only single dimension arrays are supported here.");
            }
            else if (destinationArray.Length < Length)
            {
                throw new ArgumentOutOfRangeException(nameof(destinationArray), "Destination array was not long enough.");
            }

            for (int index = 0; index < destinationArray.Length; index++)
            {
                destinationArray[index] = GetPoint(WydMath.IndexTo3D(index, _Size));
            }
        }

        public void CollapseRecursive()
        {
            _RootNode.CollapseRecursive();
        }

        #region Helper Methods

        // indexes:
        // bottom half quadrant indexes:
        // 1 3
        // 0 2
        // top half quadrant indexes:
        // 5 7
        // 4 6
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DetermineOctant(float extent, ref float x, ref float y, ref float z, out int octant)
        {
            octant = 0;

            if (x >= extent)
            {
                x -= extent;
                octant += 1;
            }

            if (y >= extent)
            {
                y -= extent;
                octant += 4;
            }

            if (z >= extent)
            {
                z -= extent;
                octant += 2;
            }
        }

        #endregion
    }
}
