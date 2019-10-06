#region

using System.Collections.Generic;

#endregion

namespace Wyd.Compression
{
    public static class RunLengthCompression
    {
        public struct Node<T>
        {
            public ushort RunLength { get; private set; }
            public T Value { get; private set; }

            public Node(ushort runLength, T value)
            {
                RunLength = runLength;
                Value = value;
            }

            public Node<T> Initialise(ushort runLength, T value)
            {
                RunLength = runLength;
                Value = value;

                return this;
            }
        }

        public static IEnumerable<Node<ushort>> Compress(IEnumerable<ushort> initialArray, ushort firstValue)
        {
            ushort currentRun = 0;
            ushort lastUnmatchedValue = firstValue;

            foreach (ushort value in initialArray)
            {
                if ((value == lastUnmatchedValue) && (currentRun < ushort.MaxValue))
                {
                    currentRun += 1;
                    continue;
                }

                yield return new Node<ushort>(currentRun, lastUnmatchedValue);

                lastUnmatchedValue = value;
                currentRun = 0;
            }
        }
    }
}
