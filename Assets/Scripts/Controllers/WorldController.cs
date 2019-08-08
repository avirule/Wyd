using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Environment.Terrain;
using Environment.Terrain.Generation;
using Environment.Terrain.Generation.Noise;
using Environment.Terrain.Generation.Noise.Perlin;
using Logging;
using NLog;
using Static;
using UnityEngine;

namespace Controllers
{
    public class WorldController : MonoBehaviour
    {
        private Vector3Int _FollowedCurrentChunk;
        private int _BlockDiameter;
        private Vector3Int _LastGenerationPoint;
        private int _WorldGenDiameter;

        public BlockController BlockController;
        public Dictionary<Vector3Int, WorldChunk> Chunks;
        public Transform FollowedTransform;
        public NoiseMap NoiseMap;
        public float WorldGenLacunarity;
        public int WorldGenOctaves;
        public float WorldGenPersistence;
        public int WorldGenRadius;
        public float WorldGenScale;
        public string WorldGenSeed;

        public WorldSeed WorldSeed;

        public void Awake()
        {
            _FollowedCurrentChunk = new Vector3Int(0, 0, 0);
            CheckFollowerChangedChunk();

            WorldSeed = new WorldSeed(WorldGenSeed);
            Chunks = new Dictionary<Vector3Int, WorldChunk>();
            _WorldGenDiameter = (WorldGenRadius * 2) + 1;
            _BlockDiameter = _WorldGenDiameter * Chunk.Size.x;
        }

        public void Start()
        {
            StartCoroutine(GenerateNoiseMap(_FollowedCurrentChunk));
            BuildChunkRadius();
        }

        private void FixedUpdate()
        {
            CheckFollowerChangedChunk();
        }

        private void CheckFollowerChangedChunk()
        {
            Vector3Int chunkPosition = GetChunkPositionFromGlobal(FollowedTransform.transform.position).ToInt();
            chunkPosition.y = 0;

            if (chunkPosition == _FollowedCurrentChunk)
            {
                return;
            }

            _FollowedCurrentChunk = chunkPosition;

            Vector3Int absDifference = (_FollowedCurrentChunk - _LastGenerationPoint).Abs();

            int doubleChunkSize = Chunk.Size.x * 2;

            if ((absDifference.x < doubleChunkSize) && (absDifference.z < doubleChunkSize))
            {
                return;
            }

            ExecuteFollowerChangedChunk();
        }

        private void ExecuteFollowerChangedChunk()
        {
            _LastGenerationPoint = _FollowedCurrentChunk;

            CullChunks();

            StartCoroutine(GenerateNoiseMap(_FollowedCurrentChunk));

            BuildChunkRadius();
        }

        private void CullChunks()
        {
            foreach (Vector3Int position in CheckCullableChunks())
            {
                Destroy(Chunks[position].ChunkObject);
                Chunks.Remove(position);

                RegenerateAdjacentChunks(position);
            }
        }

        private void RegenerateAdjacentChunks(Vector3Int position)
        {
            for (int x = -1; x < 2; x++)
            {
                // skip middle chunk
                if (x == 0)
                {
                    continue;
                }

                Vector3Int xModifiedPosition = position + new Vector3Int(Chunk.Size.x * x, 0, 0);

                if (!Chunks.ContainsKey(xModifiedPosition))
                {
                    continue;
                }

                StartCoroutine(Chunks[xModifiedPosition].Chunk.Generate(false));
            }

            for (int z = -1; z < 2; z++)
            {
                // skip middle chunk
                if (z == 0)
                {
                    continue;
                }

                Vector3Int zModifiedPosition = position + new Vector3Int(0, 0, Chunk.Size.z * z);

                if (!Chunks.ContainsKey(zModifiedPosition))
                {
                    continue;
                }

                StartCoroutine(Chunks[zModifiedPosition].Chunk.Generate(false));
            }
        }

        private IEnumerable<Vector3Int> CheckCullableChunks()
        {
            foreach (WorldChunk worldChunk in Chunks.Values.ToList())
            {
                if (Mathv.ContainsVector3Int(NoiseMap.Bounds, worldChunk.Position))
                {
                    continue;
                }

                yield return worldChunk.Position;
            }
        }

        private void BuildChunkRadius()
        {
            // +1 to include player's chunk
            for (int x = -WorldGenRadius; x < (WorldGenRadius + 1); x++)
            {
                for (int z = -WorldGenRadius; z < (WorldGenRadius + 1); z++)
                {
                    Vector3Int pos = new Vector3Int(x * Chunk.Size.x, 0, z * Chunk.Size.x) + _FollowedCurrentChunk;

                    if ((Chunks == null) || Chunks.ContainsKey(pos))
                    {
                        continue;
                    }

                    BuildChunk(pos);
                }
            }
        }

        private void BuildChunk(Vector3Int position)
        {
            GameObject chunkObject = Instantiate(Resources.Load<GameObject>(@"Environment\Terrain\Chunk"), position,
                Quaternion.identity);
            WorldChunk worldChunk = new WorldChunk(chunkObject);

            Chunks.Add(worldChunk.Position, worldChunk);

            StartCoroutine(Chunks[worldChunk.Position].Chunk.Generate(true));
            RegenerateAdjacentChunks(worldChunk.Position);
        }

        private IEnumerator GenerateNoiseMap(Vector3Int offset)
        {
            // over-generate noise map size to avoid array index overflows
            NoiseMap = new NoiseMap(null, _FollowedCurrentChunk,
                new Vector3Int(_BlockDiameter + Chunk.Size.x, 0, _BlockDiameter + Chunk.Size.z));

            PerlinNoiseGenerator perlinNoiseGenerator =
                new PerlinNoiseGenerator(offset, WorldSeed, NoiseMap.Bounds.size, WorldGenOctaves,
                    WorldGenScale, WorldGenPersistence, WorldGenLacunarity);
            perlinNoiseGenerator.Start();

            yield return new WaitUntil(() => perlinNoiseGenerator.Update());

            NoiseMap.Map = perlinNoiseGenerator.Map;
            NoiseMap.Ready = true;
        }

        public float[][] GetNoiseHeightsByPosition(Vector3Int position, Vector3Int size)
        {
            if (!Mathv.ContainsVector3Int(NoiseMap.Bounds, position))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Failed to retrieve noise map by offset: offset ({position.x}, {position.z}) outside of noise map.");
                return null;
            }

            Vector3Int indexes = position - NoiseMap.Bounds.min;
            float[][] noiseMap = new float[size.x][];
            
            for (int x = 0; x < noiseMap.Length; x++)
            {
                noiseMap[x] = new float[size.z];

                for (int z = 0; z < noiseMap[0].Length; z++)
                {
                    try
                    {
                        noiseMap[x][z] = NoiseMap.Map[indexes.x + x][indexes.z + z];
                    }
                    catch (IndexOutOfRangeException)
                    {
                    }
                }
            }

            return noiseMap;
        }

        public static Vector3 GetChunkPositionFromGlobal(Vector3 globalPosition)
        {
            return globalPosition.Divide(Chunk.Size).Floor().Multiply(Chunk.Size);
        }

        public string GetBlockAtPosition(Vector3Int position)
        {
            Vector3Int chunkPosition = GetChunkPositionFromGlobal(position).ToInt();
            Chunk refChunk;

            try
            {
                if (!Chunks.ContainsKey(chunkPosition))
                {
                    return string.Empty;
                }

                refChunk = Chunks[chunkPosition].Chunk;

                if ((refChunk == null) || (refChunk.Blocks == null))
                {
                    return string.Empty;
                }
            }
            catch (KeyNotFoundException)
            {
                // I believe this happens when the collection is edited in another thread
                // between when the dictionary keys are checked and the actual reference is obtained
                return string.Empty;
            }

            // prevents DivideByZero exception
            Vector3Int localPosition = (position - chunkPosition).Abs();

            // todo possible optimization
            if ((refChunk.Blocks.Length <= localPosition.x) ||
                (refChunk.Blocks[localPosition.x].Length <= localPosition.y) ||
                (refChunk.Blocks[localPosition.x][localPosition.y].Length <= localPosition.z))
            {
                return string.Empty;
            }

            string block = refChunk.Blocks[localPosition.x][localPosition.y][localPosition.z] ?? string.Empty;

            return block;
        }
    }
}