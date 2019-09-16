#region

using Game.World.Blocks;
using UnityEngine;

#endregion

namespace Game.World.Chunks.BuildingJob
{
    public class ChunkBuildingJobAccents : ChunkBuildingJob
    {
        private Vector3 _Position;
        private Block[] _Blocks;

        public void Set(Vector3 position, Block[] blocks)
        {
            _Position = position;
            _Blocks = blocks;
        }

        public void Generate()
        {
        }
    }
}
