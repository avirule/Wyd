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
            new ObjectCache<ChunkGenerationDispatcher>(null, null, true);

        public static readonly Vector3Int BiomeNoiseSize = new Vector3Int(32 * 16, 256, 32 * 16);
        public static readonly Vector3Int Size = new Vector3Int(32, 256, 32);
        public static readonly int YIndexStep = Size.x * Size.z;


        #region INSTANCE MEMBERS

        private Bounds _Bounds;
        private Action _PendingAction;
        private Transform _SelfTransform;
        private IEntity _CurrentLoader;
        private Block[] _Blocks;
        private Mesh _Mesh;
        private bool _Visible;
        private bool _RenderShadows;
        private ChunkGenerationDispatcher _ChunkGenerationDispatcher;

        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;

        public Vector3 Position { get; private set; }

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

        #endregion


        #region UNITY BUILT-INS

        private void Awake()
        {
            _SelfTransform = transform;
            Position = _SelfTransform.position;
            UpdateBounds();
            _Blocks = new Block[Size.Product()];
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
            if (WorldController.Current.IsOnBorrowedUpdateTime())
            {
                return;
            }

            _ChunkGenerationDispatcher.SynchronousContextUpdate();
        }

        private void OnDestroy()
        {
            Destroy(_Mesh);
            OnDestroyed(this, new ChunkChangedEventArgs(_Bounds, Directions.CardinalDirectionsVector3));
        }

        #endregion


        public void RequestMeshUpdate()
        {
            _ChunkGenerationDispatcher.RequestMeshUpdate();
        }


        #region ACTIVATION STATE

        public void Activate(Vector3 position)
        {
            _SelfTransform.position = Position = position;
            UpdateBounds();
            ConfigureDispatcher();
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
            _Visible = _RenderShadows = false;

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
            _ChunkGenerationDispatcher = ChunkGenerationDispatcherCache.RetrieveItem();
            _ChunkGenerationDispatcher.Set(_Bounds, ref _Blocks, ref _Mesh);
            _ChunkGenerationDispatcher.BlocksChanged += OnBlocksChanged;
            _ChunkGenerationDispatcher.MeshChanged += OnMeshChanged;
        }

        private void CacheDispatcher()
        {
            _ChunkGenerationDispatcher.BlocksChanged -= OnBlocksChanged;
            _ChunkGenerationDispatcher.MeshChanged -= OnMeshChanged;
            _ChunkGenerationDispatcher.Reset();
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

        public void PlaceBlockAt(Vector3 globalPosition, ushort id)
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

        public bool TryPlaceBlockAt(Vector3 globalPosition, ushort id)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                return false;
            }

            int localPosition1d = ConvertGlobalPositionToLocal1D(globalPosition);

            _Blocks[localPosition1d].Initialise(id);
            RequestMeshUpdate();

            OnBlocksChanged(this,
                new ChunkChangedEventArgs(_Bounds, DetermineDirectionsForNeighborUpdate(globalPosition)));
            return true;
        }

        public void RemoveBlockAt(Vector3 globalPosition)
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

        public bool TryRemoveBlockAt(Vector3 globalPosition, out Block block)
        {
            block = default;

            if (!_Bounds.Contains(globalPosition))
            {
                return false;
            }

            int localPosition1d = ConvertGlobalPositionToLocal1D(globalPosition);

            if (_Blocks[localPosition1d].Id == BlockController.BLOCK_EMPTY_ID)
            {
                return false;
            }

            // this should create a copy of the item
            block = _Blocks[localPosition1d];
            _Blocks[localPosition1d].Initialise(BlockController.BLOCK_EMPTY_ID);
            RequestMeshUpdate();

            OnBlocksChanged(this,
                new ChunkChangedEventArgs(_Bounds, DetermineDirectionsForNeighborUpdate(globalPosition)));
            return true;
        }

        #endregion


        #region INTERNAL STATE CHECKS

        private static bool IsWithinLoaderRange(Vector3 difference)
        {
            return difference.AllLessThanOrEqual(Size
                                                 * (OptionsController.Current.RenderDistance
                                                    + OptionsController.Current.PreLoadChunkDistance));
        }

        private static bool IsWithinRenderDistance(Vector3 difference)
        {
            return difference.AllLessThanOrEqual(Size * OptionsController.Current.RenderDistance);
        }

        private static bool IsWithinShadowsDistance(Vector3 difference)
        {
            return difference.AllLessThanOrEqual(Size * OptionsController.Current.ShadowDistance);
        }

        #endregion


        #region HELPER METHODS

        private void UpdateBounds()
        {
            _Bounds.SetMinMax(Position, Position + Size);
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

        public byte[] Serialize()
        {
            // 4 bytes for runlength and value
            List<RunLengthCompression.Node<ushort>> nodes = GetCompressed().ToList();
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

        public IEnumerable<RunLengthCompression.Node<ushort>> GetCompressed()
        {
            return RunLengthCompression.Compress(GetBlocksAsIds(), _Blocks[0].Id);
        }

        private IEnumerable<ushort> GetBlocksAsIds()
        {
            return _Blocks.Select(block => block.Id);
        }

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
