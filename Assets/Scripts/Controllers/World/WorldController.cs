#region

using System;
using Environment.Terrain;
using Environment.Terrain.Generation;
using Noise;
using Static;
using UnityEngine;

#endregion

namespace Controllers.World
{
    public class WorldController : MonoBehaviour
    {
        /// <summary>
        ///     This is referenced OFTEN in SYNCHRONOUS CONTEXT. DO NOT USE IN ASYNCHRONOUS CONTEXTS.
        /// </summary>
        public static TimeSpan WorldTickRate;

        public static long InitialTick;

        public float TicksPerSecond;
        public WorldGenerationSettings WorldGenerationSettings;
        public ChunkController ChunkController;
        public NoiseMap NoiseMap;
        public Transform ChunkLoader;
        public Vector3Int ChunkLoaderCurrentChunk;

        private void Awake()
        {
            WorldTickRate = TimeSpan.FromSeconds(1d / TicksPerSecond);

            ChunkLoaderCurrentChunk = default;
            Chunk.Size = WorldGenerationSettings.ChunkSize;
            
            NoiseMap = new NoiseMap(null, ChunkLoaderCurrentChunk,
                new Vector3Int((WorldGenerationSettings.Diameter + 1) * Chunk.Size.x, 0,
                    (WorldGenerationSettings.Diameter + 1) * Chunk.Size.z));
            
            CheckChunkLoaderChangedChunk();
        }

        private void Start()
        {
            InitialTick = DateTime.Now.Ticks;

            NoiseMap.Generate(ChunkLoaderCurrentChunk, NoiseMap.Bounds.size, WorldGenerationSettings);

            EnqueueBuildChunkArea(ChunkLoaderCurrentChunk, WorldGenerationSettings.Radius);
        }

        private void Update()
        {
            CheckChunkLoaderChangedChunk();
        }

        public static Vector3 GetWorldChunkOriginFromGlobalPosition(Vector3 globalPosition)
        {
            return globalPosition.Divide(Chunk.Size).Floor().Multiply(Chunk.Size);
        }

        public Block GetBlockAtPosition(Vector3Int position)
        {
            return ChunkController.GetBlockAtPosition(position);
        }


        #region ON UPDATE()

        private void CheckChunkLoaderChangedChunk()
        {
            Vector3Int chunkPosition =
                GetWorldChunkOriginFromGlobalPosition(ChunkLoader.transform.position).ToInt();
            chunkPosition.y = 0;

            if (chunkPosition == ChunkLoaderCurrentChunk)
            {
                return;
            }

            ChunkLoaderCurrentChunk = chunkPosition;
            UpdateChunkLoadArea();
        }

        private void UpdateChunkLoadArea()
        {
            NoiseMap.Generate(ChunkLoaderCurrentChunk, NoiseMap.Bounds.size, WorldGenerationSettings);


            EnqueueBuildChunkArea(ChunkLoaderCurrentChunk, WorldGenerationSettings.Radius);
        }

        public void EnqueueBuildChunkArea(Vector3Int origin, int radius)
        {
            // +1 to include player's chunk
            for (int x = -radius; x < (radius + 1); x++)
            {
                for (int z = -radius; z < (radius + 1); z++)
                {
                    Vector3Int position = origin + Chunk.Size.Multiply(new Vector3Int(x, 0, z));

                    ChunkController.BuildChunkQueue.Enqueue(position);
                }
            }
        }

        #endregion
    }
}