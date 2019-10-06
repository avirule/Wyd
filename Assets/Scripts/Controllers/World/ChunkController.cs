#region

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        private static readonly ObjectCache<ChunkGenerator> ChunkGeneratorsCache =
            new ObjectCache<ChunkGenerator>(true);

        private static readonly ObjectCache<BlockAction> BlockActionsCache =
            new ObjectCache<BlockAction>(true, true, 1024);

        public static readonly Vector3Int Size = new Vector3Int(32, 256, 32);
        public static readonly int YIndexStep = Size.x * Size.z;


        #region INSTANCE MEMBERS

        private Bounds _Bounds;
        private Transform _SelfTransform;
        private IEntity _CurrentLoader;
        private Block[] _Blocks;
        private Mesh _Mesh;
        private bool _Visible;
        private bool _RenderShadows;
        private ChunkGenerator _ChunkGenerator;
        private Stack<BlockAction> _BlockActions;
        private HashSet<Vector3> _BlockActionLocalPositions;

        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;

        public Vector3 Position => _Bounds.min;
        public ChunkGenerator.GenerationStep GenerationStep => _ChunkGenerator.CurrentStep;

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
            _BlockActions = new Stack<BlockAction>();
            _BlockActionLocalPositions = new HashSet<Vector3>();

            MeshFilter.sharedMesh = _Mesh;

            foreach (Material material in MeshRenderer.materials)
            {
                material.SetTexture(TextureController.MainTexPropertyID, TextureController.Current.TerrainTexture);
            }

            _Visible = MeshRenderer.enabled;

            ConfigureGenerator();

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

            if (_BlockActions.Count > 0)
            {
                ProcessBlockActions();
            }
            else
            {
                _ChunkGenerator.SynchronousContextUpdate();
            }
        }

        private void OnDestroy()
        {
            Destroy(_Mesh);
            OnDestroyed(this, new ChunkChangedEventArgs(_Bounds, Directions.CardinalDirectionsVector3));
        }

        #endregion

        private void ConfigureGenerator()
        {
            if (!ChunkGeneratorsCache.TryRetrieveItem(out _ChunkGenerator))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Chunk at position {Position} unable to retrieve a {nameof(ChunkGenerator)} from cache. This is most likely a serious error.");
            }

            _ChunkGenerator.Set(_Bounds, ref _Blocks, ref _Mesh);
            _ChunkGenerator.BlocksChanged += OnBlocksChanged;
            _ChunkGenerator.MeshChanged += OnMeshChanged;
        }


        private void CacheGenerator()
        {
            _ChunkGenerator.BlocksChanged -= OnBlocksChanged;
            _ChunkGenerator.MeshChanged -= OnMeshChanged;
            _ChunkGenerator.Unset();
            ChunkGeneratorsCache.CacheItem(ref _ChunkGenerator);
        }

        public void RequestMeshUpdate()
        {
            _ChunkGenerator.RequestMeshUpdate();
        }

        private void ProcessBlockActions()
        {
            while ((_BlockActions.Count > 0) && WorldController.Current.IsInSafeFrameTime())
            {
                BlockAction blockAction = _BlockActions.Pop();
                _BlockActionLocalPositions.Remove(blockAction.LocalPosition);
                int localPosition1d = blockAction.LocalPosition.To1D(Size);

                if (localPosition1d < _Blocks.Length)
                {
                    _Blocks[localPosition1d].Initialise(blockAction.Id);
                    RequestMeshUpdate();
                    OnBlocksChanged(this,
                        new ChunkChangedEventArgs(_Bounds,
                            DetermineDirectionsForNeighborUpdate(blockAction.LocalPosition)));
                }

                BlockActionsCache.CacheItem(ref blockAction);
            }
        }


        #region ACTIVATION STATE

        public void Activate(Vector3 position)
        {
            _SelfTransform.position = position;
            UpdateBounds();
            _Visible = MeshRenderer.enabled;
            ConfigureGenerator();
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

            _BlockActions.Clear();
            _BlockActionLocalPositions.Clear();
            _Bounds = default;

            CacheGenerator();
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

        #endregion


        #region TRY GET / PLACE / REMOVE BLOCKS

        public ref Block GetBlockAt(Vector3 globalPosition)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(globalPosition), globalPosition,
                    $"Given position `{globalPosition}` exists outside of local bounds.");
            }

            return ref _Blocks[(globalPosition - Position).To1D(Size, true)];
        }

        public bool TryGetBlockAt(Vector3 globalPosition, out Block block)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                block = default;
                return false;
            }

            block = _Blocks[(globalPosition - Position).To1D(Size, true)];
            return true;
        }

        public bool BlockExistsAt(Vector3 globalPosition)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(globalPosition), globalPosition,
                    $"Given position `{globalPosition}` exists outside of local bounds.");
            }

            return _Blocks[(globalPosition - Position).To1D(Size, true)].Id != BlockController.Air.Id;
        }

        public void ImmediatePlaceBlockAt(Vector3 globalPosition, ushort id)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(globalPosition), globalPosition,
                    $"Given position `{globalPosition}` outside of local bounds.");
            }


            Vector3 localPosition = globalPosition - Position;
            _Blocks[localPosition.To1D(Size, true)].Initialise(id);
            RequestMeshUpdate();

            OnBlocksChanged(this,
                new ChunkChangedEventArgs(_Bounds, DetermineDirectionsForNeighborUpdate(localPosition)));
        }

        public bool TryPlaceBlockAt(Vector3 globalPosition, ushort id)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                return false;
            }

            return TryAllocateBlockAction(globalPosition - Position, id);
        }

        public void ImmediateRemoveBlockAt(Vector3 globalPosition)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(globalPosition), globalPosition,
                    $"Given position `{globalPosition}` outside of local bounds.");
            }

            Vector3 localPosition = globalPosition - Position;
            _Blocks[localPosition.To1D(Size, true)] = BlockController.Air;
            RequestMeshUpdate();

            OnBlocksChanged(this,
                new ChunkChangedEventArgs(_Bounds, DetermineDirectionsForNeighborUpdate(localPosition)));
        }

        public bool TryRemoveBlockAt(Vector3 globalPosition)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                return false;
            }

            return TryAllocateBlockAction(globalPosition - Position, BlockController.Air.Id);
        }

        private bool TryAllocateBlockAction(Vector3 localPosition, ushort id)
        {
            // todo this vvvv
            if (_BlockActionLocalPositions.Contains(localPosition)
                || !_Bounds.Contains(localPosition)
                || !BlockActionsCache.TryRetrieveItem(out BlockAction blockAction))
            {
                return false;
            }

            blockAction.Initialise(localPosition, id);
            _BlockActions.Push(blockAction);
            _BlockActionLocalPositions.Add(localPosition);
            return true;
        }

        #endregion


        #region HELPER METHODS

        private void UpdateBounds()
        {
            Vector3 position = _SelfTransform.position;
            _Bounds.SetMinMax(position, position + Size);
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static IEnumerable<Vector3> DetermineDirectionsForNeighborUpdate(Vector3 localPosition)
        {
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

            _ChunkGenerator.SkipBuilding(true);
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
