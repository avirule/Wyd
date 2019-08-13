#region

using System.Collections;
using Environment.Terrain;
using Environment.Terrain.Generation;
using Environment.Terrain.Generation.Noise;
using Static;
using Threading.Generation;
using UnityEngine;

#endregion

namespace Controllers.World
{
    public class WorldController : MonoBehaviour
    {
        /// <summary>
        ///     This is referenced OFTEN in SYNCHRONOUS CONTEXT. DO NOT USE IN ASYNCHRONOUS CONTEXTS.
        /// </summary>
        public static float WorldTickRate;

        public float TicksPerSecond;
        public WorldGenerationSettings WorldGenerationSettings;
        public ChunkController ChunkController;
        public NoiseMap NoiseMap;
        public Transform ChunkLoader;
        public Vector3Int ChunkLoaderCurrentChunk;

        private void Awake()
        {
            WorldTickRate = 1f / TicksPerSecond;
            ChunkLoaderCurrentChunk = default;
            Chunk.Size = WorldGenerationSettings.ChunkSize;
            CheckChunkLoaderChangedChunk();
        }

        private void Start()
        {
            StartCoroutine(GenerateNoiseMap(ChunkLoaderCurrentChunk));

            EnqueueBuildChunkArea(ChunkLoaderCurrentChunk, WorldGenerationSettings.Radius);
        }

        private void Update()
        {
            CheckChunkLoaderChangedChunk();
        }


        private IEnumerator GenerateNoiseMap(Vector3Int offset)
        {
            // over-generate noise map size to avoid array index overflows
            NoiseMap = new NoiseMap(null, ChunkLoaderCurrentChunk,
                new Vector3Int((WorldGenerationSettings.Diameter + 1) * Chunk.Size.x, 0,
                    (WorldGenerationSettings.Diameter + 1) * Chunk.Size.z));

            PerlinNoiseGenerator perlinNoiseGenerator =
                new PerlinNoiseGenerator(offset, NoiseMap.Bounds.size, WorldGenerationSettings);
            perlinNoiseGenerator.Start();

            yield return new WaitUntil(() => perlinNoiseGenerator.Update());

            NoiseMap.Map = perlinNoiseGenerator.Map;
            NoiseMap.Ready = true;
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
            StartCoroutine(GenerateNoiseMap(ChunkLoaderCurrentChunk));

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