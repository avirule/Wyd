using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Controllers.Game;
using Environment.Terrain;
using Environment.Terrain.Generation;
using Environment.Terrain.Generation.Noise;
using Environment.Terrain.Generation.Noise.Perlin;
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

        public BlockController BlockController;

        public Dictionary<Vector3Int, Chunk> Chunks;
        public Queue<Vector3Int> PendingChunkRemovals;
        public bool ChunksChanged;
        public Transform FollowedTransform;
        public MeshCollider MeshCollider;
        public bool Meshed;
        public MeshFilter MeshFilter;
        public bool Meshing;
        public bool Building;
        public NoiseMap NoiseMap;
        public WorldGenerationSettings WorldGenerationSettings;

        private void Awake()
        {
            _FollowedCurrentChunk = new Vector3Int(0, 0, 0);
            CheckFollowerChangedChunk();

            Chunks = new Dictionary<Vector3Int, Chunk>();
            PendingChunkRemovals = new Queue<Vector3Int>();
            ChunksChanged = Meshed = false;
        }

        private void Start()
        {
            _BlockDiameter = WorldGenerationSettings.Diameter * Chunk.Size.x;

            StartCoroutine(GenerateNoiseMap(_FollowedCurrentChunk));
            StartCoroutine(BuildChunkRadius());
        }

        private void Update()
        {
            CheckFollowerChangedChunk();
            AllocateDestroyableChunks();

            if (PendingChunkRemovals.Count > 0)
            {
                StartCoroutine(DestroyPendingChunks());
            }
        }

        private void FixedUpdate()
        {
            CheckMeshingAndUpdate();
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

            StartCoroutine(BuildChunkRadius());
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

        private (bool, bool) CheckAllChunksGeneratedOrMeshed()
        {
            return (Chunks.Values.All(chunk => chunk.Generated),
                Chunks.Values.All(worldChunk => worldChunk.Meshed));
        }

        private void CheckMeshingAndUpdate()
        {
            (bool allGenerated, bool allMeshed) = CheckAllChunksGeneratedOrMeshed();

            if (!allGenerated)
            {
                return;
            }

            if (!allMeshed || ChunksChanged)
            {
                foreach (Chunk chunk in Chunks.Values)
                {
                    if (chunk.Destroy || ((chunk.Meshing || chunk.Meshed) && !ChunksChanged))
                    {
                        continue;
                    }

                    StopCoroutine(chunk.GenerateMesh());
                    StartCoroutine(chunk.GenerateMesh());
                }

                ChunksChanged = Meshed = false;
            }

            if (Meshed || Meshing || !allMeshed || Building || PendingChunkRemovals.Count > 0)
            {
                return;
            }

            StartCoroutine(GenerateCombinedMesh());
        }

        private IEnumerator GenerateCombinedMesh()
        {
            Meshed = false;
            Meshing = true;

            int index = 0;
            CombineInstance[] combines = new CombineInstance[Chunks.Count];

            foreach (Chunk chunk in Chunks.Values)
            {
// todo measure function time and restrict to one frame
                
                CombineInstance combine = new CombineInstance
                {
                    mesh = chunk.Mesh,
                    transform = Matrix4x4.TRS(chunk.Position, Quaternion.identity, new Vector3(1f, 1f, 1f))
                };

                combines[index] = combine;
                index++;

                yield return null;
            }

            CombineMeshesAndAssign(combines);

            Meshing = false;
            Meshed = true;
        }

        private void CombineMeshesAndAssign(CombineInstance[] combines)
        {
            MeshFilter.mesh = new Mesh {indexFormat = IndexFormat.UInt32};
            MeshFilter.mesh.CombineMeshes(combines, true, true);
            MeshCollider.sharedMesh = MeshFilter.sharedMesh;
        }

        #endregion

        
        #region Chunk Building / Culling

        private void AllocateDestroyableChunks()
        {
            foreach (Vector3Int position in Chunks.Keys)
            {
                if (Mathv.ContainsVector3Int(NoiseMap.Bounds, position))
                {
                    continue;
                }
                
                PendingChunkRemovals.Enqueue(position);
            }
        }

        private IEnumerator DestroyPendingChunks()
        {
            while (PendingChunkRemovals.Count > 0)
            {
                if (Meshing)
                {
                    yield return null;
                }
                
                DestroyChunk(PendingChunkRemovals.Dequeue());
            }
        }
        
        private void DestroyChunk(Vector3Int position)
        {
            if (!Chunks.ContainsKey(position))
            {
                return;
            }
            
            DestroyChunk(Chunks[position]);
        }
        
        private void DestroyChunk(Chunk chunk)
        {
            chunk.Destroy = true;
            Destroy(chunk.Mesh);
            Chunks.Remove(chunk.Position);

            ChunksChanged = true;
        }
        
        private IEnumerator BuildChunkRadius()
        {
            Building = true;
            
            // +1 to include player's chunk
            for (int x = -WorldGenerationSettings.Radius; x < (WorldGenerationSettings.Radius + 1); x++)
            {
                for (int z = -WorldGenerationSettings.Radius; z < (WorldGenerationSettings.Radius + 1); z++)
                {
                    // if world is meshing (i.e. enumerating through chunks) wait and continue when complete
                    if (Meshing)
                    {
                        // to restrict z from enumerating forward and breaking out of the for loop
                        z--;
                        yield return null;
                        continue;
                    }
                    
                    Vector3Int pos = new Vector3Int(x * Chunk.Size.x, 0, z * Chunk.Size.x) + _FollowedCurrentChunk;

                    if ((Chunks == null) || Chunks.ContainsKey(pos))
                    {
                        continue;
                    }

                    CreateChunk(pos);
                }
            }

            Building = false;
        }

        private void CreateChunk(Vector3Int position)
        {
            Chunk chunk = new Chunk(this, BlockController, position);

            Chunks.Add(chunk.Position, chunk);
            StartCoroutine(chunk.GenerateBlocks());

            ChunksChanged = true;
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

            Chunks.TryGetValue(chunkPosition, out Chunk chunk);

            // prevents DivideByZero exception
            Vector3Int localPosition = (position - chunkPosition).Abs();

            string block = chunk?.Blocks[localPosition.x][localPosition.y][localPosition.z] ?? string.Empty;

            return block;
        }

        #endregion
    }
}