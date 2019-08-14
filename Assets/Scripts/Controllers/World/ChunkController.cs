#region

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Controllers.Game;
using Environment.Terrain;
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
        private Queue<Chunk> _CachedChunks;
        private bool _ProcessingChunkQueue;

        public List<Chunk> Chunks;
        public Queue<Vector3Int> BuildChunkQueue;
        public int CurrentCacheSize => _CachedChunks.Count;
        public bool AllChunksGenerated => Chunks.All(chunk => chunk.Generated);
        public bool AllChunksMeshed => Chunks.All(chunk => chunk.Meshed);

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

            _ChunkObject = Resources.Load<Chunk>(@"Environment\Terrain\Chunk");
            _CachedChunks = new Queue<Chunk>();
            _FrameTimeLimiter = new Stopwatch();
            _ProcessingChunkQueue = false;

            Chunks = new List<Chunk>();
            BuildChunkQueue = new Queue<Vector3Int>();
        }

        public void Update()
        {
            _FrameTimeLimiter.Restart();

            if ((BuildChunkQueue.Count > 0) && !_ProcessingChunkQueue)
            {
                ProcessBuildChunkQueue();
            }

            if (AllChunksGenerated && AllChunksMeshed)
            {
                DeactivateOutOfBoundsChunks();
            }

            // cull chunks down to half the maximum when idle
            if (_CachedChunks.Count > (OptionsController.Current.MaximumChunkCacheSize / 2))
            {
                CullCachedChunks();
            }

            _FrameTimeLimiter.Stop();
        }

        #region CHUNK BUILDING

        public void ProcessBuildChunkQueue()
        {
            _ProcessingChunkQueue = true;

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

                Chunks.Add(chunk);

                // ensures that neighbours update their meshes to cull newly out of sight faces
                FlagNeighborsPendingUpdate(chunk.Position);

                if (_FrameTimeLimiter.Elapsed.TotalSeconds > OptionsController.Current.MaximumInternalFrameTime)
                {
                    break;
                }
            }

            _ProcessingChunkQueue = false;
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

        private void DeactivateOutOfBoundsChunks()
        {
            for (int i = Chunks.Count - 1; i >= 0; i--)
            {
                Vector3Int difference = (Chunks[i].Position - WorldController.Current.ChunkLoaderCurrentChunk).Abs();

                if ((difference.x <= ((WorldController.Current.WorldGenerationSettings.Radius + 1) * Chunk.Size.x)) &&
                    (difference.z <= ((WorldController.Current.WorldGenerationSettings.Radius + 1) * Chunk.Size.z)))
                {
                    continue;
                }

                DeactivateChunk(Chunks[i]);

                if (_FrameTimeLimiter.Elapsed.TotalSeconds > OptionsController.Current.MaximumInternalFrameTime)
                {
                    break;
                }
            }
        }

        private void DeactivateChunk(Chunk chunk)
        {
            _CachedChunks.Enqueue(chunk);
            Chunks.Remove(chunk);
            FlagNeighborsPendingUpdate(chunk.Position);
            chunk.Deactivate();
        }

        private void CullCachedChunks()
        {
            // controller will cull chunks down to half the maximum when idle
            while (_CachedChunks.Count > (OptionsController.Current.MaximumChunkCacheSize / 2))
            {
                Chunk chunk = _CachedChunks.Dequeue();
                Destroy(chunk.gameObject);

                // continue culling if the amount of cached chunks is greater than the maximum
                if ((_FrameTimeLimiter.Elapsed.TotalSeconds >
                     OptionsController.Current.MaximumInternalFrameTime) &&
                    (OptionsController.Current.CacheCullingAggression == CacheCullingAggression.Passive))
                {
                    return;
                }
            }
        }

        #endregion


        #region MISC

        public bool CheckChunkExistsAtPosition(Vector3Int position)
        {
            // reverse for loop to avoid collection modified from thread errors
            for (int i = Chunks.Count - 1; i >= 0; i--)
            {
                if ((Chunks.Count <= i) || (Chunks[i] == default))
                {
                    continue;
                }

                if (Chunks[i].Position == position)
                {
                    return true;
                }
            }

            return false;
        }

        public Chunk GetChunkAtPosition(Vector3Int position)
        {
            // reverse for loop to avoid collection modified from thread errors
            for (int i = Chunks.Count - 1; i >= 0; i--)
            {
                if ((Chunks.Count <= i) || (Chunks[i] == default))
                {
                    continue;
                }

                if (Chunks[i].Position == position)
                {
                    return Chunks[i];
                }
            }

            return default;
        }

        public Block GetBlockAtPosition(Vector3Int position)
        {
            Vector3Int chunkPosition = WorldController.GetWorldChunkOriginFromGlobalPosition(position).ToInt();

            Chunk chunk = GetChunkAtPosition(chunkPosition);

            if ((chunk == null) || !chunk.Generated)
            {
                return default;
            }

            Vector3Int localPosition = (position - chunkPosition).Abs();
            int localPosition1d =
                localPosition.x + (Chunk.Size.x * (localPosition.y + (Chunk.Size.y * localPosition.z)));

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