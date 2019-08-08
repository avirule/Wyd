using System;
using System.Collections;
using System.Collections.Concurrent;
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
using UnityEngine.Rendering;

namespace Controllers
{
    public class WorldController : MonoBehaviour
    {
        private int _BlockDiameter;
        private Vector3Int _FollowedCurrentChunk;
        private Vector3Int _LastGenerationPoint;

        public ConcurrentDictionary<Vector3Int, WorldChunk> Chunks;

        public bool ChunksChanged;
        public Transform FollowedTransform;
        public MeshCollider MeshCollider;
        public bool Meshed;
        public MeshFilter MeshFilter;
        public NoiseMap NoiseMap;
        public WorldGenerationSettings WorldGenerationSettings;

        public void Awake()
        {
            _FollowedCurrentChunk = new Vector3Int(0, 0, 0);
            CheckFollowerChangedChunk();

            Chunks = new ConcurrentDictionary<Vector3Int, WorldChunk>();
            ChunksChanged = Meshed = false;
        }

        public void Start()
        {
            _BlockDiameter = WorldGenerationSettings.Diameter * Chunk.Size.x;

            StartCoroutine(GenerateNoiseMap(_FollowedCurrentChunk));
            BuildWorldChunkRadius();
        }

        public void Update()
        {
            (bool allGenerated, bool allMeshed) = CheckAllChunksGeneratedOrMeshed();

            if (!allGenerated)
            {
                return;
            }

            if (!allMeshed || ChunksChanged)
            {
                foreach (WorldChunk worldChunk in Chunks.Values)
                {
                    if ((worldChunk.Chunk.Meshing || worldChunk.Chunk.Meshed) && !ChunksChanged)
                    {
                        continue;
                    }

                    StopCoroutine(worldChunk.Chunk.GenerateMesh());
                    StartCoroutine(worldChunk.Chunk.GenerateMesh());
                }

                ChunksChanged = Meshed = false;
            }

            if (Meshed || !allMeshed)
            {
                return;
            }

            GenerateCombinedMesh();
        }

        private void FixedUpdate()
        {
            CheckFollowerChangedChunk();
        }

        private void CheckFollowerChangedChunk()
        {
            Vector3Int chunkPosition =
                GetWorldChunkOriginFromGlobalPosition(FollowedTransform.transform.position).ToInt();
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

            CullWorldChunks();

            StartCoroutine(GenerateNoiseMap(_FollowedCurrentChunk));

            BuildWorldChunkRadius();
        }

        #region Meshin

        private (bool, bool) CheckAllChunksGeneratedOrMeshed()
        {
            return (Chunks.Values.All(worldChunk => worldChunk.Chunk.Generated),
                Chunks.Values.All(worldChunk => worldChunk.Chunk.Meshed));
        }

        private void GenerateCombinedMesh()
        {
            Meshed = false;

            int index = 0;
            CombineInstance[] combines = new CombineInstance[Chunks.Count];

            foreach (WorldChunk worldChunk in Chunks.Values)
            {
                CombineInstance combine = new CombineInstance
                {
                    mesh = worldChunk.Chunk.MeshFilter.sharedMesh,
                    transform = worldChunk.GameObject.transform.localToWorldMatrix
                };
                worldChunk.Chunk.MeshFilter.gameObject.SetActive(false);

                combines[index] = combine;

                index++;
            }

            MeshFilter.mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            MeshFilter.mesh.CombineMeshes(combines, true, true);
            MeshCollider.sharedMesh = MeshFilter.sharedMesh;

            Meshed = true;
        }

        #endregion

        #region WorldChunk Building / Culling

        private void CullWorldChunks()
        {
            foreach (WorldChunk worldChunk in CheckCullableWorldChunks())
            {
                Destroy(worldChunk.GameObject);
                Chunks.TryRemove(worldChunk.Position, out WorldChunk _);

                ChunksChanged = true;
            }
        }

        private IEnumerable<WorldChunk> CheckCullableWorldChunks()
        {
            return Chunks.Values.Where(worldChunk => !Mathv.ContainsVector3Int(NoiseMap.Bounds, worldChunk.Position));
        }

        private void BuildWorldChunkRadius()
        {
            // +1 to include player's chunk
            for (int x = -WorldGenerationSettings.Radius; x < (WorldGenerationSettings.Radius + 1); x++)
            {
                for (int z = -WorldGenerationSettings.Radius; z < (WorldGenerationSettings.Radius + 1); z++)
                {
                    Vector3Int pos = new Vector3Int(x * Chunk.Size.x, 0, z * Chunk.Size.x) + _FollowedCurrentChunk;

                    if ((Chunks == null) || Chunks.ContainsKey(pos))
                    {
                        continue;
                    }

                    BuildWorldChunk(pos);
                }
            }
        }

        private void BuildWorldChunk(Vector3Int position)
        {
            GameObject chunkObject = Instantiate(Resources.Load<GameObject>(@"Environment\Terrain\Chunk"), position,
                Quaternion.identity);
            WorldChunk worldChunk = new WorldChunk(chunkObject);
            worldChunk.GameObject.transform.parent = transform;

            Chunks.TryAdd(worldChunk.Position, worldChunk);
            StartCoroutine(worldChunk.Chunk.GenerateBlocks());

            ChunksChanged = true;
        }

        #endregion

        #region Noise

        private IEnumerator GenerateNoiseMap(Vector3Int offset)
        {
            // over-generate noise map size to avoid array index overflows
            NoiseMap = new NoiseMap(null, _FollowedCurrentChunk,
                new Vector3Int(_BlockDiameter + Chunk.Size.x, 0, _BlockDiameter + Chunk.Size.z));

            PerlinNoiseGenerator perlinNoiseGenerator =
                new PerlinNoiseGenerator(offset, NoiseMap.Bounds.size, WorldGenerationSettings);
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

        #endregion

        #region WorldChunk Misc

        public static Vector3 GetWorldChunkOriginFromGlobalPosition(Vector3 globalPosition)
        {
            return globalPosition.Divide(Chunk.Size).Floor().Multiply(Chunk.Size);
        }

        public string GetBlockAtPosition(Vector3Int position)
        {
            Vector3Int chunkPosition = GetWorldChunkOriginFromGlobalPosition(position).ToInt();

            Chunks.TryGetValue(chunkPosition, out WorldChunk worldChunk);

            if (worldChunk == null)
            {
                return string.Empty;
            }

            // prevents DivideByZero exception
            Vector3Int localPosition = (position - chunkPosition).Abs();

            string block = worldChunk.Chunk.Blocks[localPosition.x][localPosition.y][localPosition.z] ?? string.Empty;

            return block;
        }

        #endregion
    }
}