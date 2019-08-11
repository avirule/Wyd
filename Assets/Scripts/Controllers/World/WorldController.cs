#region

using System.Collections;
using Environment.Terrain;
using Environment.Terrain.Generation;
using Environment.Terrain.Generation.Noise;
using Static;
using Threading.Generation;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Controllers.World
{
    public class WorldController : MonoBehaviour
    {
        public const float WORLD_TICK_RATE = 1f / 90f;
        public static int ChunkLoaderSnapDistance;

        private Vector3Int _ChunkLoaderCurrentChunk;
        private Vector3Int _LastChunkLoadPosition;
        private bool _RegenerateNoise;

        public ChunkController ChunkController;
        public Transform ChunkLoader;
        public NoiseMap NoiseMap;
        public WorldGenerationSettings WorldGenerationSettings;
        public int ShadowRadius;

        private void Awake()
        {
            Chunk.Size = WorldGenerationSettings.ChunkSize;
            ChunkLoaderSnapDistance = Chunk.Size.x * 2;
            _ChunkLoaderCurrentChunk = new Vector3Int(0, 0, 0);
            CheckChunkLoaderChangedChunk();
        }

        private void Start()
        {
            StartCoroutine(GenerateNoiseMap(_ChunkLoaderCurrentChunk));

            EnqueueBuildChunkArea(_ChunkLoaderCurrentChunk, WorldGenerationSettings.Radius);
        }

        private void Update()
        {
            CheckChunkLoaderChangedChunk();
            CheckMeshingAndTick();
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

        private IEnumerator GenerateNoiseMap(Vector3Int offset)
        {
            _RegenerateNoise = false;
            // over-generate noise map size to avoid array index overflows
            NoiseMap = new NoiseMap(null, _ChunkLoaderCurrentChunk,
                new Vector3Int((WorldGenerationSettings.Diameter + 1) * Chunk.Size.x, 0,
                    (WorldGenerationSettings.Diameter + 1) * Chunk.Size.z));

            PerlinNoiseGenerator perlinNoiseGenerator =
                new PerlinNoiseGenerator(offset, NoiseMap.Bounds.size, WorldGenerationSettings);
            perlinNoiseGenerator.Start();

            yield return new WaitUntil(() => perlinNoiseGenerator.Update() || _RegenerateNoise);

            if (_RegenerateNoise)
            {
                perlinNoiseGenerator.Abort();
                yield break;
            }

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

            if (chunkPosition == _ChunkLoaderCurrentChunk)
            {
                return;
            }

            _ChunkLoaderCurrentChunk = chunkPosition;

            Vector3Int absDifference = (_ChunkLoaderCurrentChunk - _LastChunkLoadPosition).Abs();

            if ((absDifference.x < ChunkLoaderSnapDistance) && (absDifference.z < ChunkLoaderSnapDistance))
            {
                return;
            }

            UpdateChunkLoadArea();
        }

        private void UpdateChunkLoadArea()
        {
            _LastChunkLoadPosition = _ChunkLoaderCurrentChunk;

            _RegenerateNoise = true;
            StartCoroutine(GenerateNoiseMap(_ChunkLoaderCurrentChunk));

            EnqueueBuildChunkArea(_ChunkLoaderCurrentChunk, WorldGenerationSettings.Radius);

            foreach (Chunk chunk in ChunkController.Chunks)
            {
                Vector3Int difference = (chunk.Position - _ChunkLoaderCurrentChunk).Abs();

                if ((difference.x > (ShadowRadius * Chunk.Size.x)) || (difference.z > (ShadowRadius * Chunk.Size.z)))
                {
                    chunk.MeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    chunk.MeshRenderer.receiveShadows = false;
                }
                else
                {
                    chunk.MeshRenderer.shadowCastingMode = ShadowCastingMode.On;
                    chunk.MeshRenderer.receiveShadows = true;
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