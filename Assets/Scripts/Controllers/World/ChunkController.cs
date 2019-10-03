#region

using System;
using System.Collections.Generic;
using System.Linq;
using Compression;
using Controllers.State;
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
    public class ChunkController : MonoBehaviour
    {
        private static readonly ObjectCache<ChunkGenerationDispatcher> ChunkGenerationDispatcherCache =
            new ObjectCache<ChunkGenerationDispatcher>(true);

        private static readonly ObjectCache<BlockAction> BlockActionsCache =
            new ObjectCache<BlockAction>(true, true, 1024);

        public static readonly Vector3Int BiomeNoiseSize = new Vector3Int(32 * 16, 256, 32 * 16);
        public static readonly Vector3Int Size = new Vector3Int(16, 256, 16);
        public static readonly int YIndexStep = Size.x * Size.z;


        #region INSTANCE MEMBERS

        private Bounds _Bounds;
        private Transform _SelfTransform;
        private IEntity _CurrentLoader;
        private Block[] _Blocks;
        private Mesh _Mesh;
        private bool _Visible;
        private bool _RenderShadows;
        private ChunkGenerationDispatcher _ChunkGenerationDispatcher;
        private Stack<BlockAction> _BlockActions;
        private HashSet<Vector3> _BlockActionGlobalPositions;
        private TimeSpan _SafeFrameTime; // this value changes often, try to don't set it

        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;

        public Vector3 Position => _Bounds.min;

        public ChunkGenerationDispatcher.GenerationStep GenerationStep => _ChunkGenerationDispatcher.CurrentStep;

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

#if UNITY_EDITOR

        public bool StepInto;
        public bool PopulatePublicBlocks;
        public bool Remesh;
        public ushort[] Blocks;
        public int VertexCount;

#endif

        #endregion


        #region UNITY BUILT-INS

        private void Awake()
        {
            _SelfTransform = transform;
            UpdateBounds();
            _Blocks = new Block[Size.Product()];
            _BlockActions = new Stack<BlockAction>();
            _BlockActionGlobalPositions = new HashSet<Vector3>();
            _Mesh = new Mesh();
            ConfigureDispatcher();

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
#if UNITY_EDITOR

            if (StepInto)
            {
            }

            if (PopulatePublicBlocks)
            {
                Blocks = _Blocks.Select(block => block.Id).ToArray();

                PopulatePublicBlocks = false;
            }

            if (Remesh)
            {
                _ChunkGenerationDispatcher.RequestMeshUpdate();

                Remesh = false;
            }

            if (_Mesh.vertexCount != VertexCount)
            {
                VertexCount = _Mesh.vertexCount;
            }

            if (WorldController.Current.StepIntoSelectedChunkStep
                && (GenerationStep == WorldController.Current.SelectedStep))
            {
            }

            if (!WorldController.Current.IgnoreInternalFrameLimit
                && WorldController.Current.IsInSafeFrameTime())
            {
                return;
            }

            if (!ProcessBlockActions())
            {
                _ChunkGenerationDispatcher.SynchronousContextUpdate();
            }

#else
            if (WorldController.Current.IsInSafeFrameTime())
            {
                return;
            }

            if (!ProcessBlockActions())
            {
                _ChunkGenerationDispatcher.SynchronousContextUpdate();
            }
#endif
        }

        private void OnDestroy()
        {
            Destroy(_Mesh);
            OnDestroyed(this, new ChunkChangedEventArgs(_Bounds, Directions.CardinalDirectionsVector3));
        }

        #endregion


        private bool ProcessBlockActions()
        {
            if (_BlockActions.Count > 0)
            {
                do
                {
                    WorldController.Current.GetRemainingSafeFrameTime(out _SafeFrameTime);

                    BlockAction blockAction = _BlockActions.Pop();
                    _BlockActionGlobalPositions.Remove(blockAction.GlobalPosition);

                    int localPosition1d = ConvertGlobalPositionToLocal1D(blockAction.GlobalPosition);

                    if (localPosition1d < _Blocks.Length)
                    {
                        _Blocks[localPosition1d].Initialise(blockAction.Id);
                        RequestMeshUpdate();
                        OnBlocksChanged(this,
                            new ChunkChangedEventArgs(_Bounds,
                                DetermineDirectionsForNeighborUpdate(blockAction.GlobalPosition)));
                    }

                    BlockActionsCache.CacheItem(ref blockAction);
                } while ((_SafeFrameTime > TimeSpan.Zero) && (_BlockActions.Count > 0));

                return true;
            }

            return false;
        }

        public void RequestMeshUpdate()
        {
            _ChunkGenerationDispatcher.RequestMeshUpdate();
        }


        #region ACTIVATION STATE

        public void Activate(Vector3 position)
        {
            _SelfTransform.position = position;
            UpdateBounds();
            ConfigureDispatcher();
            _Visible = MeshRenderer.enabled;
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

            if (_ChunkGenerationDispatcher != default)
            {
                CacheDispatcher();
            }

            _Bounds = default;
            StopAllCoroutines();
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

        private void ConfigureDispatcher()
        {
            if (!ChunkGenerationDispatcherCache.TryRetrieveItem(out _ChunkGenerationDispatcher))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Chunk at position {Position} unable to retrieve a ChunkGenerationDispatcher from cache. This is most likely a serious error.");
            }

            _ChunkGenerationDispatcher.Set(_Bounds, _Blocks, ref _Mesh);
            _ChunkGenerationDispatcher.BlocksChanged += OnBlocksChanged;
            _ChunkGenerationDispatcher.MeshChanged += OnMeshChanged;
        }

        private void CacheDispatcher()
        {
            _ChunkGenerationDispatcher.BlocksChanged -= OnBlocksChanged;
            _ChunkGenerationDispatcher.MeshChanged -= OnMeshChanged;
            _ChunkGenerationDispatcher.Unset();
            ChunkGenerationDispatcherCache.CacheItem(ref _ChunkGenerationDispatcher);
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

            return ref _Blocks[ConvertGlobalPositionToLocal1D(globalPosition)];
        }

        public bool TryGetBlockAt(Vector3 globalPosition, out Block block)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                block = default;
                return false;
            }

            block = _Blocks[ConvertGlobalPositionToLocal1D(globalPosition)];
            return true;
        }

        public bool BlockExistsAt(Vector3 globalPosition)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(globalPosition), globalPosition,
                    $"Given position `{globalPosition}` exists outside of local bounds.");
            }

            int localPosition1d = ConvertGlobalPositionToLocal1D(globalPosition);

            return _Blocks[localPosition1d].Id != BlockController.BLOCK_EMPTY_ID;
        }

        public void ImmediatePlaceBlockAt(Vector3 globalPosition, ushort id)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(globalPosition), globalPosition,
                    $"Given position `{globalPosition}` outside of local bounds.");
            }

            int localPosition1d = ConvertGlobalPositionToLocal1D(globalPosition);

            _Blocks[localPosition1d].Initialise(id);
            RequestMeshUpdate();

            OnBlocksChanged(this,
                new ChunkChangedEventArgs(_Bounds, DetermineDirectionsForNeighborUpdate(globalPosition)));
        }

        public bool TryPlaceBlockAt(Vector3 globalPosition, ushort id) => TryAllocateBlockAction(globalPosition, id);

        public void ImmediateRemoveBlockAt(Vector3 globalPosition)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(globalPosition), globalPosition,
                    $"Given position `{globalPosition}` outside of local bounds.");
            }

            int localPosition1d = ConvertGlobalPositionToLocal1D(globalPosition);

            _Blocks[localPosition1d].Initialise(BlockController.BLOCK_EMPTY_ID);
            RequestMeshUpdate();

            OnBlocksChanged(this,
                new ChunkChangedEventArgs(_Bounds, DetermineDirectionsForNeighborUpdate(globalPosition)));
        }

        public bool TryRemoveBlockAt(Vector3 globalPosition) =>
            TryAllocateBlockAction(globalPosition, BlockController.BLOCK_EMPTY_ID);

        private bool TryAllocateBlockAction(Vector3 globalPosition, ushort id)
        {
            // todo this vvvv
            if (_BlockActionGlobalPositions.Contains(globalPosition)
                || !_Bounds.Contains(globalPosition)
                || !BlockActionsCache.TryRetrieveItem(out BlockAction blockAction))
            {
                return false;
            }

            blockAction.Initialise(globalPosition, id);
            _BlockActions.Push(blockAction);
            _BlockActionGlobalPositions.Add(globalPosition);
            return true;
        }

        #endregion


        #region HELPER METHODS

        private void UpdateBounds()
        {
            Vector3 position = _SelfTransform.position;
            _Bounds.SetMinMax(position, position + Size);
        }

        private int ConvertGlobalPositionToLocal1D(Vector3 globalPosition)
        {
            Vector3 localPosition = (globalPosition - Position).Abs();
            return localPosition.To1D(Size);
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

            _ChunkGenerationDispatcher.SkipBuilding(true);
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
