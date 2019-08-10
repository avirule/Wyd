using System.Collections;
using Environment.Terrain;
using Environment.Terrain.Generation;
using Environment.Terrain.Generation.Noise;
using Environment.Terrain.Generation.Noise.Perlin;
using Static;
using UnityEngine;

namespace Controllers.World
{
    public class WorldController : MonoBehaviour
    {
        public const float WORLD_TICK_RATE = 1f / 60f;

        private int _BlockDiameter;
        private Vector3Int _FollowedCurrentChunk;
        private Vector3Int _LastGenerationPoint;

        public ChunkController ChunkController;
        public Transform FollowedTransform;
        public MeshCollider MeshCollider;
        public MeshFilter MeshFilter;
        public WorldGenerationSettings WorldGenerationSettings;

        private void Awake()
        {
            _FollowedCurrentChunk = new Vector3Int(0, 0, 0);
            CheckFollowerChangedChunk();
        }

        private void Start()
        {
            _BlockDiameter = WorldGenerationSettings.Diameter * Chunk.Size.x;

            StartCoroutine(GenerateNoiseMap(_FollowedCurrentChunk));
            StartCoroutine(ChunkController.BuildChunkArea(_FollowedCurrentChunk, WorldGenerationSettings.Radius));
        }

        private void Update()
        {
            CheckFollowerChangedChunk();
            CheckMeshingAndTick();
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

            StartCoroutine(GenerateNoiseMap(_FollowedCurrentChunk));

            StartCoroutine(ChunkController.BuildChunkArea(_FollowedCurrentChunk, WorldGenerationSettings.Radius));
        }

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

        #endregion


        #region Meshing

        private void CheckMeshingAndTick()
        {
            if (!NoiseMap.Ready)
            {
                return;
            }
            
            ChunkController.Tick(NoiseMap.Bounds);

            if (ChunkController.Meshing ||
                !ChunkController.Meshed ||
                (ChunkController.AggregateMesh == null) ||
                ((MeshFilter.sharedMesh != null) &&
                 (ChunkController.AggregateMesh.vertexCount == MeshFilter.sharedMesh.vertexCount)))
            {
                return;
            }

            CombineMeshesAndAssign(ChunkController.AggregateMesh);
        }

        private void CombineMeshesAndAssign(Mesh aggregateMesh)
        {
            MeshFilter.mesh = aggregateMesh;
            MeshCollider.sharedMesh = MeshFilter.sharedMesh;
        }

        #endregion

        #region WorldChunk Misc

        public static Vector3 GetWorldChunkOriginFromGlobalPosition(Vector3 globalPosition)
        {
            return globalPosition.Divide(Chunk.Size).Floor().Multiply(Chunk.Size);
        }

        public Block GetBlockAtPosition(Vector3Int position)
        {
            return ChunkController.GetBlockAtPosition(position);
        }

        #endregion
    }
}