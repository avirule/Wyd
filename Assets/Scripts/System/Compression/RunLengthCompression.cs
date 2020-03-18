#region

using System;
using System.Collections.Generic;

#endregion

namespace Wyd.System.Compression
{
    public static class RunLengthCompression
    {
        public static IEnumerable<RLENode<T>> Compress<T>(IEnumerable<T> initialArray, T firstValue)
            where T : IEquatable<T>
        {
            uint currentRun = 0;
            T currentValue = firstValue;

            foreach (T value in initialArray)
            {
                if (currentValue.Equals(value) && (currentRun < uint.MaxValue))
                {
                    currentRun += 1;
                    continue;
                }

                yield return new RLENode<T>(currentRun, currentValue);

                currentValue = value;
                currentRun = 1;
            }
        }

        public static IEnumerable<T> Decompress<T>(IEnumerable<RLENode<T>> nodes) where T : IEquatable<T>
        {
            foreach (RLENode<T> node in nodes)
            {
                for (int i = 0; i < node.RunLength; i++)
                {
                    yield return node.Value;
                }
            }
        }

        public static IEnumerable<T> DecompressLinkedList<T>(LinkedList<RLENode<T>> nodes) where T : IEquatable<T>
        {
            LinkedListNode<RLENode<T>> currentNode = nodes.First;

            while (currentNode != null)
            {
                for (int i = 0; i < currentNode.Value.RunLength; i++)
                {
                    yield return currentNode.Value.Value;
                }
                
                currentNode = currentNode.Next;
            }
        }
    }

    public class RLENode<T> : IEquatable<RLENode<T>> where T : IEquatable<T>
    {
        public uint RunLength { get; set; }
        public T Value { get; }

        public RLENode(uint runLength, T value)
        {
            RunLength = runLength;
            Value = value;
        }

        public bool Equals(RLENode<T> other) =>
            (RunLength == other?.RunLength) && Value.Equals(other.Value);

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return (obj.GetType() == GetType()) && Equals((RLENode<T>)obj);
        }

        public override int GetHashCode() =>
            unchecked(((int)RunLength * 397) ^ EqualityComparer<T>.Default.GetHashCode(Value));
    }
}
