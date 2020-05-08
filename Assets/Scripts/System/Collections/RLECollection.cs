#region

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Wyd.Game.World.Chunks.Generation;

#endregion

namespace Wyd.System.Collections
{
    public class RLECollection<T> : INodeCollection<T> where T : unmanaged, IEquatable<T>
    {
        private readonly LinkedList<RLENode<T>> _RLENodes;
        private uint _CubicDepth;

        public T Value => _RLENodes.First.Value.Value;
        public bool IsUniform => _RLENodes.Count == 1;
        public int Length { get; }

        public RLECollection(uint cubicDepth, T initialValue)
        {
            _CubicDepth = cubicDepth;

            // fill node with initial value
            _RLENodes = new LinkedList<RLENode<T>>();
            _RLENodes.AddFirst(new RLENode<T>((uint)math.pow(cubicDepth, 3), initialValue));
        }

        public T GetPoint(float3 point)
        {
            int index = WydMath.PointToIndex(point, GenerationConstants.CHUNK_SIZE);
            LinkedListNode<RLENode<T>> currentNode = _RLENodes.First;

            for (uint i = 0; (i < index) && ((currentNode = currentNode.Next) != null);)
            {
                uint totalRunLength = i + currentNode.Value.RunLength;

                if (totalRunLength >= index)
                {
                    return currentNode.Value.Value;
                }
                else
                {
                    i = totalRunLength;
                }
            }

            return default;
        }

        public void SetPoint(float3 point, T value)
        {
            int index = WydMath.PointToIndex(point, GenerationConstants.CHUNK_SIZE);
            LinkedListNode<RLENode<T>> currentNode = _RLENodes.First;

            for (uint i = 0; i < index;)
            {
                if (currentNode == null)
                {
                    return;
                }

                uint totalRunLength = i + currentNode.Value.RunLength;

                if (totalRunLength >= index)
                {
                    if (currentNode.Value.Value.GetHashCode() == value.GetHashCode())
                    {
                        // value already set, so just exit
                        return;
                    }

                    uint difference = (uint)(totalRunLength - index);
                    // -1 to account for our inserted node
                    uint leftOverRunLength = currentNode.Value.RunLength - difference;

                    if (leftOverRunLength == 0)
                    {
                        LinkedListNode<RLENode<T>> interimNode = currentNode.Previous;
                        _RLENodes.Remove(currentNode);
                        currentNode = interimNode;

                        if (currentNode.Value.GetHashCode() == value.GetHashCode())
                        {
                            currentNode.Value.RunLength += 1;
                        }

                        if (currentNode.Value.GetHashCode() == currentNode.Next.Value.GetHashCode())
                        {
                            currentNode.Value.RunLength += currentNode.Next.Value.RunLength;
                            _RLENodes.Remove(currentNode.Next);
                        }

                        return;
                    }
                    else
                    {
                        currentNode.Value.RunLength -= difference;
                    }


                    LinkedListNode<RLENode<T>> insertionNode = _RLENodes.AddAfter(currentNode, new RLENode<T>(1, value));

                    if (leftOverRunLength == 0)
                    {
                        _RLENodes.AddAfter(insertionNode, new RLENode<T>(leftOverRunLength - 1, currentNode.Value.Value));
                    }

                    return;
                }
                else
                {
                    i += currentNode.Value.RunLength;
                    currentNode = currentNode.Next;
                }
            }

            //Log.Information(RunLengthCompression.GetTotalRunLength(_RLENodes).ToString());
        }

        public IEnumerable<T> GetAllData() => RunLengthCompression.DecompressLinkedList(_RLENodes);

        public void CopyTo(T[] destinationArray)
        {
            throw new NotImplementedException();
        }
    }
}
