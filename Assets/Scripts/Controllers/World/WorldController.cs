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
        public static Vector3Int ChunkLoaderCurrentChunk;

        public static float WorldTickRate;
        public static int ChunkLoaderSnapDistance;

        private Vector3Int _LastChunkLoadPosition;

        public ChunkController ChunkController;
        public Transform ChunkLoader;
        public NoiseMap NoiseMap;
        public WorldGenerationSettings WorldGenerationSettings;

        private void Awake()
        {
            WorldTickRate = Time.maximumDeltaTime;
            ChunkLoaderCurrentChunk = default;
            Chunk.Size = WorldGenerationSettings.ChunkSize;
            ChunkLoaderSnapDistance = Chunk.Size.x * 2;
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
            CheckMeshingAndTick();
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

            Vector3Int absDifference = (ChunkLoaderCurrentChunk - _LastChunkLoadPosition).Abs();

            if ((absDifference.x < ChunkLoaderSnapDistance) && (absDifference.z <= ChunkLoaderSnapDistance))
            {
                return;
            }

            UpdateChunkLoadArea();
        }

        private void UpdateChunkLoadArea()
        {
            _LastChunkLoadPosition = ChunkLoaderCurrentChunk;

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
                    ChunkController.BuildChunkQueue.Enqueue(origin + Chunk.Size.Multiply(new Vector3Int(x, 0, z)));
                }
            }
        }
        
        private void CheckMeshingAndTick()
        {
            if (!NoiseMap.Ready)
            {
                return;
            }

            ChunkController.Tick(NoiseMap.Bounds);
        }

        #endregion
    }
}