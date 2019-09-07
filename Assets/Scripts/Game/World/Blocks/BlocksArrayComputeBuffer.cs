using Game.World.Chunks;
using UnityEngine;

namespace Game.World.Blocks
{
    public class BlocksArrayComputeBuffer
    {
        private static readonly Vector3Int DefaultSize = Chunk.Size;
        public readonly ComputeBuffer Buffer;
        public float[] Data { get; private set; }

        public BlocksArrayComputeBuffer()
        {
            Buffer = new ComputeBuffer(DefaultSize.Product(), 4);
            Data = new float[DefaultSize.Product()];
        }

        public BlocksArrayComputeBuffer(int size)
        {
            Buffer = new ComputeBuffer(size, 4);
            Data = new float[size];
        }

        public void GetData()
        {
            Buffer.GetData(Data);
        }
    }
}