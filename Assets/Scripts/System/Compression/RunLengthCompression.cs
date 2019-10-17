#region

using System.Collections.Generic;

#endregion

namespace Wyd.System.Compression
{
    public static class RunLengthCompression
    {
        public static IEnumerable<RLENode<int>> Compress(IEnumerable<int> initialArray, int firstValue)
        {
            int currentRun = 0;
            int lastUnmatchedValue = firstValue;

            foreach (int value in initialArray)
            {
                if ((value == lastUnmatchedValue) && (currentRun < int.MaxValue))
                {
                    currentRun += 1;
                    continue;
                }

                yield return new RLENode<int>(currentRun, lastUnmatchedValue);

                lastUnmatchedValue = value;
                currentRun = 0;
            }
        }
    }

    public class RLENode<T>
    {
        public int RunLength { get; set; }
        public T Value { get; set; }

        public RLENode(int runLength, T value)
        {
            RunLength = runLength;
            Value = value;
        }
    }
}
