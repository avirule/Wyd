#region

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Controllers.Entity;
using Controllers.Game;
using Game.Terrain;
using Static;
using UnityEngine;

#endregion

namespace Controllers.World
{
    public sealed class ChunkController : MonoBehaviour
    {
        public static ChunkController Current;

        private Stopwatch _FrameTimeLimiter;
        private Chunk _ChunkObject;
        private Dictionary<Vector3Int, Chunk> _Chunks;
        private Queue<Chunk> _CachedChunks;
        private Queue<Chunk> _DeactivationQueue;

        public Queue<Vector3Int> BuildChunkQueue;
        public int ActiveChunksCount => _Chunks.Count;
        public int CachedChunksCount => _CachedChunks.Count;
        public bool AllChunksBuilt => _Chunks.All(kvp => kvp.Value.Built);
        public bool AllChunksMeshed => _Chunks.All(kvp => kvp.Value.Meshed);

        private void Awake()
        {
            if ((Current != null) && (Current != this))
            {
                Destroy(gameObject);
            }
            else
            {
                Current = this;
            }

            _ChunkObject = Resources.Load<Chunk>(@"Terrain\Chunk");
            _CachedChunks = new Queue<Chunk>();
            _DeactivationQueue = new Queue<Chunk>();
            _FrameTimeLimiter = new Stopwatch();

            _Chunks = new Dictionary<Vector3Int, Chunk>();
            BuildChunkQueue = new Queue<Vector3Int>();
        }

        private void Start()
        {
            PlayerController.Current.ChunkChanged += MarkOutOfBoundsChunksForDeactivation;
        }

        private void Update()
        {
            if (Input.GetKey(KeyCode.R))
            {
                foreach (KeyValuePair<Vector3Int, Chunk> kvp in _Chunks)
                {
                    kvp.Value.PendingMeshUpdate = true;
                }
            }

            _FrameTimeLimiter.Restart();

            if (BuildChunkQueue.Count > 0)
            {
                ProcessBuildChunkQueue();
            }

            if (_DeactivationQueue.Count > 0)
            {
                ProcessDeactivationQueue();
            }

            // if maximum chunk cache size is not zero...
            // cull chunks down to half the maximum when idle
            if ((OptionsController.Current.MaximumChunkCacheSize != 0) &&
                (_CachedChunks.Count > (OptionsController.Current.MaximumChunkCacheSize / 2)))
            {
                CullCachedChunks();
            }

            _FrameTimeLimiter.Stop();
        }

        private void AddChunk(Chunk chunk)
        {
            _Chunks.Add(chunk.Position, chunk);
        }

        private void RemoveChunk(Chunk chunk)
        {
            if (!_Chunks.ContainsKey(chunk.Position))
            {
                return;
            }

            _Chunks.Remove(chunk.Position);
        }

        #region CHUNK BUILDING

        public void ProcessBuildChunkQueue()
        {
            while (BuildChunkQueue.Count > 0)
            {
                Vector3Int position = BuildChunkQueue.Dequeue();

                if (CheckChunkExistsAtPosition(position))
                {
                    continue;
                }

                Chunk chunk;

                if (_CachedChunks.Count > 0)
                {
                    chunk = _CachedChunks.Dequeue();
                    chunk.Activate(position);
                }
                else
                {
                    chunk = Instantiate(_ChunkObject, position, Quaternion.identity, transform);
                }

                AddChunk(chunk);

                // ensures that neighbours update their meshes to cull newly out of sight faces
                FlagNeighborsPendingUpdate(chunk.Position);

                if (_FrameTimeLimiter.Elapsed.TotalSeconds > OptionsController.Current.MaximumInternalFrameTime)
                {
                    break;
                }
            }
        }

        private void FlagNeighborsPendingUpdate(Vector3Int position)
        {
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0)
                {
                    continue;
                }

                Vector3Int modifiedPosition = position + new Vector3Int(x * Chunk.Size.x, 0, 0);
                Chunk chunkAtPosition = GetChunkAtPosition(modifiedPosition);

                if ((chunkAtPosition == default) || chunkAtPosition.PendingMeshUpdate || !chunkAtPosition.Active)
                {
                    continue;
                }

                chunkAtPosition.PendingMeshUpdate = true;
            }

            for (int z = -1; z <= 1; z++)
            {
                if (z == 0)
                {
                    continue;
                }

                Vector3Int modifiedPosition = position + new Vector3Int(0, 0, z * Chunk.Size.z);
                Chunk chunkAtPosition = GetChunkAtPosition(modifiedPosition);

                if ((chunkAtPosition == default) || chunkAtPosition.PendingMeshUpdate || !chunkAtPosition.Active)
                {
                    continue;
                }

                chunkAtPosition.PendingMeshUpdate = true;
            }
        }

        #endregion


        #region CHUNK DISABLING

        private void MarkOutOfBoundsChunksForDeactivation(object sender, Vector3Int chunkPosition)
        {
            foreach (KeyValuePair<Vector3Int, Chunk> kvp in _Chunks)
            {
                Vector3Int difference = (kvp.Value.Position - chunkPosition).Abs();

                if ((difference.x <= ((WorldController.Current.WorldGenerationSettings.Radius + 1) * Chunk.Size.x)) &&
                    (difference.z <= ((WorldController.Current.WorldGenerationSettings.Radius + 1) * Chunk.Size.z)))
                {
                    continue;
                }

                _DeactivationQueue.Enqueue(kvp.Value);
            }
        }

        private void ProcessDeactivationQueue()
        {
            while (_DeactivationQueue.Count > 0)
            {
                Chunk chunk = _DeactivationQueue.Dequeue();
                DeactivateChunk(chunk);

                if (_FrameTimeLimiter.Elapsed.TotalSeconds > OptionsController.Current.MaximumInternalFrameTime)
                {
                    break;
                }
            }
        }

        private void DeactivateChunk(Chunk chunk)
        {
            _CachedChunks.Enqueue(chunk);
            RemoveChunk(chunk);
            FlagNeighborsPendingUpdate(chunk.Position);
            chunk.Deactivate();
        }

        private void CullCachedChunks()
        {
            if (OptionsController.Current.MaximumChunkCacheSize == 0)
            {
                return;
            }

            // controller will cull chunks down to half the maximum when idle
            while (_CachedChunks.Count > (OptionsController.Current.MaximumChunkCacheSize / 2))
            {
                Chunk chunk = _CachedChunks.Dequeue();
                Destroy(chunk.gameObject);

                // continue culling if the amount of cached chunks is greater than the maximum
                if ((_FrameTimeLimiter.Elapsed.TotalSeconds >
                     OptionsController.Current.MaximumInternalFrameTime) &&
                    (OptionsController.Current.ChunkCacheCullingAggression == CacheCullingAggression.Passive))
                {
                    return;
                }
            }
        }

        #endregion


        #region MISC

        public bool CheckChunkExistsAtPosition(Vector3Int position)
        {
            return _Chunks.ContainsKey(position);
        }

        // todo this function needs to be made thread-safe
        public Chunk GetChunkAtPosition(Vector3Int position)
        {
            return _Chunks.ContainsKey(position) ? _Chunks[position] : default;
        }

        public Block GetBlockAtPosition(Vector3Int position)
        {
            Vector3Int chunkPosition = WorldController.GetWorldChunkOriginFromGlobalPosition(position).ToInt();

            Chunk chunk = GetChunkAtPosition(chunkPosition);

            if ((chunk == default) || !chunk.Built)
            {
                return default;
            }

            Vector3Int localPosition = (position - chunkPosition).Abs();
            int localPosition1d = localPosition.To1D(Chunk.Size);

            if (chunk.Blocks.Length <= localPosition1d)
            {
                return default;
            }

            Block block = chunk.Blocks[localPosition1d];
            return block;
        }

        #endregion
    }
}