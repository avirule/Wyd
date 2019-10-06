#region

using System;
using System.Collections.Generic;
using System.Linq;
using Compression;
using Controllers.State;
using Extensions;
using Game;
using Game.Entities;
using Game.World.Blocks;
using Game.World.Chunks;
using Logging;
using NLog;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Controllers.World
{
    public class ChunkRegionController : MonoBehaviour
    {
        private static readonly ObjectCache<BlockAction> BlockActionsCache =
            new ObjectCache<BlockAction>(true, true, 1024);

        public static readonly Vector3Int BiomeNoiseSize = new Vector3Int(32 * 16, 256, 32 * 16);
        public static readonly Vector3Int SizeInChunks = new Vector3Int(8, 8, 8);
        public static readonly Vector3Int Size = SizeInChunks * Chunk.Size;
        public static readonly int YIndexStep = Size.x * Size.z;


        #region INSTANCE MEMBERS

        private Bounds _Bounds;
        private Transform _SelfTransform;
        private IEntity _CurrentLoader;
        private Block[] _Blocks;
        private Mesh _Mesh;
        private bool _Visible;
        private bool _RenderShadows;
        private TimeSpan _SafeFrameTime; // this value changes often, try to don't set it
        private Dictionary<Vector3, Chunk> _Chunks;
        private bool _AwaitingMeshCombining;

        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;

        public Vector3 Position => _Bounds.min;

        public ChunkGenerationDispatcher.GenerationStep AggregateGenerationStep => GetAggregateGenerationStep();

        public bool RenderShadows
        {
            get => _RenderShadows;
            set
            {
                if (_RenderShadows == value)
                {
                    return;
                }

                _RenderShadows = value;
                MeshRenderer.receiveShadows = _RenderShadows;
                MeshRenderer.shadowCastingMode = _RenderShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
        }

        public bool Visible
        {
            get => _Visible;
            set
            {
                if (_Visible == value)
                {
                    return;
                }

                _Visible = value;
                MeshRenderer.enabled = value;
            }
        }

        #endregion


        #region UNITY BUILT-INS

        private void Awake()
        {
            _SelfTransform = transform;
            UpdateBounds();
            _Blocks = new Block[Size.Product()];
            _Mesh = new Mesh();
            _Chunks = new Dictionary<Vector3, Chunk>();
            _AwaitingMeshCombining = true;

            BuildChunks();

            MeshFilter.sharedMesh = _Mesh;

            foreach (Material material in MeshRenderer.materials)
            {
                material.SetTexture(TextureController.MainTexPropertyID, TextureController.Current.TerrainTexture);
            }

            _Visible = MeshRenderer.enabled;

            // todo implement chunk ticks
//            double waitTime = TimeSpan
//                .FromTicks((DateTime.Now.Ticks - WorldController.Current.InitialTick) %
//                           WorldController.Current.WorldTickRate.Ticks)
//                .TotalSeconds;
//            InvokeRepeating(nameof(Tick), (float) waitTime, (float) WorldController.Current.WorldTickRate.TotalSeconds);
        }

        private void Start()
        {
            if (_CurrentLoader == default)
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Chunk at position {Position} has been initialized without a loader. This is possibly an error.");
            }
            else
            {
                OnCurrentLoaderChangedChunk(this, _CurrentLoader.CurrentChunk);
            }

            OptionsController.Current.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName.Equals(nameof(OptionsController.Current.ShadowDistance)))
                {
                    RenderShadows = IsWithinShadowsDistance((Position - _CurrentLoader.CurrentChunk).Abs());
                }
            };
        }

        private void Update()
        {
            if (!WorldController.Current.IsInSafeFrameTime())
            {
                return;
            }

            UpdateChunks();

            if (_AwaitingMeshCombining
                && (AggregateGenerationStep == ChunkGenerationDispatcher.GenerationStep.Complete))
            {
                ApplyAggregateMesh(ref _Mesh);
                _AwaitingMeshCombining = false;
            }
        }

        private void OnDestroy()
        {
            Destroy(_Mesh);
            OnDestroyed(this, new ChunkChangedEventArgs(_Bounds, Directions.CardinalDirectionsVector3));
        }

        #endregion


        private void BuildChunks()
        {
            // todo job this
            for (int index = 0; index < SizeInChunks.Product(); index++)
            {
                Vector3Int position = Mathv.GetIndexAsVector3Int(index, SizeInChunks) * Chunk.Size;

                if (_Chunks.ContainsKey(position))
                {
                    if (!_Chunks[position].Active)
                    {
                        _Chunks[position].Activate(position);
                    }

                    continue;
                }

                Chunk chunk = new Chunk(position);
                chunk.BlocksChanged += OnBlocksChanged;
                chunk.MeshChanged += OnMeshChanged;
                _Chunks.Add(position, chunk);
            }
        }

        private void UpdateChunks()
        {
            foreach ((Vector3 _, Chunk chunk) in _Chunks.TakeWhile(kvp => WorldController.Current.IsInSafeFrameTime()))
            {
                chunk.Update();
            }
        }

        private ChunkGenerationDispatcher.GenerationStep GetAggregateGenerationStep()
        {
            ChunkGenerationDispatcher.GenerationStep step = ChunkGenerationDispatcher.GenerationStep.Complete;

            foreach ((Vector3 _, Chunk chunk) in _Chunks)
            {
                step &= chunk.GenerationStep;
            }

            return step;
        }

        private Mesh ApplyAggregateMesh(ref Mesh mesh)
        {
            MeshData meshData = new MeshData();

            foreach ((Vector3 _, Chunk chunk) in _Chunks)
            {
                meshData.AllocateMeshData(chunk.MeshData);
            }

            meshData.ApplyToMesh(ref mesh);

            return mesh;
        }

        public void RequestMeshUpdate(Vector3 internalChunkPosition)
        {
            if (_Chunks.TryGetValue(internalChunkPosition, out Chunk chunk))
            {
                chunk.RequestMeshUpdate();
            }
        }


        #region ACTIVATION STATE

        public void Activate(Vector3 position)
        {
            _SelfTransform.position = position;
            UpdateBounds();
            _Visible = MeshRenderer.enabled;

            foreach ((Vector3 _, Chunk chunk) in _Chunks)
            {
                chunk.Activate(chunk.Position);
            }

            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            if (_Mesh != default)
            {
                _Mesh.Clear();
            }

            if (_CurrentLoader != default)
            {
                _CurrentLoader.ChunkPositionChanged -= OnCurrentLoaderChangedChunk;
                _CurrentLoader = default;
            }

            _Bounds = default;
            StopAllCoroutines();

            foreach ((Vector3 _, Chunk chunk) in _Chunks)
            {
                chunk.Deactivate();
            }

            gameObject.SetActive(false);
        }

        public void AssignLoader(IEntity loader)
        {
            if (loader == default)
            {
                return;
            }

            _CurrentLoader = loader;
            _CurrentLoader.ChunkPositionChanged += OnCurrentLoaderChangedChunk;
        }

        #endregion


        #region TRY GET / PLACE / REMOVE BLOCKS

        public ref Block GetBlockAt(Vector3 globalPosition)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(globalPosition), globalPosition,
                    $"Given position `{globalPosition}` exists outside of local bounds.");
            }

            Vector3 localPosition = globalPosition - Position;
            return ref _Chunks[localPosition].GetBlockAt(localPosition);
        }

        public bool TryGetBlockAt(Vector3 globalPosition, out Block block)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                block = default;
                return false;
            }

            Vector3 localPosition = globalPosition - Position;
            return _Chunks[localPosition].TryGetBlockAt(localPosition, out block);
        }

        public bool BlockExistsAt(Vector3 globalPosition)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(globalPosition), globalPosition,
                    $"Given position `{globalPosition}` exists outside of local bounds.");
            }

            Vector3 localPosition = globalPosition - Position;
            return _Chunks[localPosition].BlockExistsAt(localPosition);
        }

        public void ImmediatePlaceBlockAt(Vector3 globalPosition, ushort id)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(globalPosition), globalPosition,
                    $"Given position `{globalPosition}` outside of local bounds.");
            }


            Vector3 localPosition = globalPosition - Position;
            _Chunks[localPosition].ImmediatePlaceBlockAt(localPosition, id);
        }

        public bool TryPlaceBlockAt(Vector3 globalPosition, ushort id)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                return false;
            }

            Vector3 localPosition = globalPosition - Position;
            return _Chunks[localPosition].TryPlaceBlockAt(localPosition, id);
        }

        public void ImmediateRemoveBlockAt(Vector3 globalPosition)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(globalPosition), globalPosition,
                    $"Given position `{globalPosition}` outside of local bounds.");
            }

            Vector3 localPosition = globalPosition - Position;
            _Chunks[localPosition].ImmediateRemoveBlockAt(localPosition);
        }

        public bool TryRemoveBlockAt(Vector3 globalPosition)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                return false;
            }

            Vector3 localPosition = globalPosition - Position;
            return _Chunks[localPosition].BlockExistsAt(localPosition);
        }

        #endregion


        #region HELPER METHODS

        private void UpdateBounds()
        {
            Vector3 position = _SelfTransform.position;
            _Bounds.SetMinMax(position, position + Size);
        }

        private IEnumerable<Vector3> DetermineDirectionsForNeighborUpdate(Vector3 globalPosition)
        {
            Vector3 localPosition = globalPosition - Position;

            // topleft & bottomright x computation value
            float tl_br_x = localPosition.x * Size.x;
            // topleft & bottomright y computation value
            float tl_br_y = localPosition.z * Size.z;

            // topright & bottomleft left-side computation value
            float tr_bl_l = localPosition.x + localPosition.z;
            // topright & bottomleft right-side computation value
            float tr_bl_r = (Size.x + Size.z) / 2f;

            // `half` refers to the diagonal half of the chunk the point lies in.
            // If the point does not lie in a diagonal half, its a center block, and we don't need to update chunks.

            bool isInTopLeftHalf = tl_br_x > tl_br_y;
            bool isInBottomRightHalf = tl_br_x < tl_br_y;
            bool isInTopRightHalf = tr_bl_l > tr_bl_r;
            bool isInBottomLeftHalf = tr_bl_l < tr_bl_r;

            if (isInTopRightHalf && isInTopLeftHalf)
            {
                yield return Vector3.right;
            }
            else if (isInTopRightHalf && isInBottomRightHalf)
            {
                yield return Vector3.forward;
            }
            else if (isInBottomRightHalf && isInBottomLeftHalf)
            {
                yield return Vector3.left;
            }
            else if (isInBottomLeftHalf && isInTopLeftHalf)
            {
                yield return Vector3.back;
            }
            else if (!isInTopRightHalf && !isInBottomLeftHalf)
            {
                if (isInTopLeftHalf)
                {
                    yield return Vector3.back;
                    yield return Vector3.left;
                }
                else if (isInBottomRightHalf)
                {
                    yield return Vector3.back;
                    yield return Vector3.right;
                }
            }
            else if (!isInTopLeftHalf && !isInBottomRightHalf)
            {
                if (isInTopRightHalf)
                {
                    yield return Vector3.forward;
                    yield return Vector3.right;
                }
                else if (isInBottomLeftHalf)
                {
                    yield return Vector3.forward;
                    yield return Vector3.left;
                }
            }
        }

        public byte[] GetCompressedAsByteArray()
        {
            // 4 bytes for runlength and value
            List<RunLengthCompression.Node<ushort>> nodes = GetCompressedRaw().ToList();
            byte[] bytes = new byte[nodes.Count * 4];

            for (int i = 0; i < bytes.Length; i += 4)
            {
                int nodesIndex = i / 4;

                // copy runlength (ushort, 2 bytes) to position of i
                Array.Copy(BitConverter.GetBytes(nodes[nodesIndex].RunLength), 0, bytes, i, 2);
                // copy node value, also 2 bytes, to position of i + 2 bytes from runlength
                Array.Copy(BitConverter.GetBytes(nodes[nodesIndex].Value), 0, bytes, i + 2, 2);
            }

            return bytes;
        }

        public void BuildFromByteData(byte[] data)
        {
            if ((data.Length % 4) != 0)
            {
                return;
            }

            int blocksIndex = 0;
            for (int i = 0; i < data.Length; i += 4)
            {
                ushort runLength = BitConverter.ToUInt16(data, i);
                ushort value = BitConverter.ToUInt16(data, i + 2);

                for (int run = 0; run < runLength; run++)
                {
                    _Blocks[blocksIndex].Initialise(value);
                    blocksIndex += 1;
                }
            }

            // todo this v v v
            //_ChunkGenerationDispatcher.SkipBuilding(true);
        }

        public IEnumerable<RunLengthCompression.Node<ushort>> GetCompressedRaw() =>
            RunLengthCompression.Compress(GetBlocksAsIds(), _Blocks[0].Id);

        private IEnumerable<ushort> GetBlocksAsIds()
        {
            return _Blocks.Select(block => block.Id);
        }

        #endregion


        #region INTERNAL STATE CHECKS

        private static bool IsWithinLoaderRange(Vector3 difference) =>
            difference.AllLessThanOrEqual(Size
                                          * (OptionsController.Current.RenderDistance
                                             + OptionsController.Current.PreLoadChunkDistance));

        private static bool IsWithinRenderDistance(Vector3 difference) =>
            difference.AllLessThanOrEqual(Size * OptionsController.Current.RenderDistance);

        private static bool IsWithinShadowsDistance(Vector3 difference) =>
            difference.AllLessThanOrEqual(Size * OptionsController.Current.ShadowDistance);

        #endregion


        #region EVENTS

        // todo chunk load failed event

        public event EventHandler<ChunkChangedEventArgs> BlocksChanged;
        public event EventHandler<ChunkChangedEventArgs> MeshChanged;
        public event EventHandler<ChunkChangedEventArgs> DeactivationCallback;
        public event EventHandler<ChunkChangedEventArgs> Destroyed;

        protected virtual void OnBlocksChanged(object sender, ChunkChangedEventArgs args)
        {
            BlocksChanged?.Invoke(sender, args);
        }

        protected virtual void OnMeshChanged(object sender, ChunkChangedEventArgs args)
        {
            MeshChanged?.Invoke(sender, args);
        }

        protected virtual void OnDestroyed(object sender, ChunkChangedEventArgs args)
        {
            Destroyed?.Invoke(sender, args);
        }

        private void OnCurrentLoaderChangedChunk(object sender, Vector3 newChunkPosition)
        {
            if (Position == newChunkPosition)
            {
                return;
            }

            Vector3 difference = (Position - newChunkPosition).Abs();

            if (!IsWithinLoaderRange(difference))
            {
                DeactivationCallback?.Invoke(this,
                    new ChunkChangedEventArgs(_Bounds, Directions.CardinalDirectionsVector3));
                return;
            }

            Visible = IsWithinRenderDistance(difference);
            RenderShadows = IsWithinShadowsDistance(difference);
        }

        #endregion
    }
}
