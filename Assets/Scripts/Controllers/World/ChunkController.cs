#region

using System;
using System.Collections.Generic;
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

        public static readonly Vector3Int Size = new Vector3Int(16, 256, 16);
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
            MeshRenderer.material.SetTexture(TextureController.Current.MainTex,
                TextureController.Current.TerrainTexture);
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

        public Block GetBlockAt(Vector3 globalPosition)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(globalPosition), globalPosition,
                    $"Given position `{globalPosition}` exists outside of local bounds.");
            }

            int localPosition1d = ConvertGlobalPositionToLocal1D(globalPosition);

            return _Blocks[localPosition1d];
        }

        public bool TryGetBlockAt(Vector3 globalPosition, out Block block)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                block = default;
                return false;
            }

            int localPosition1d = ConvertGlobalPositionToLocal1D(globalPosition);
            block = _Blocks[localPosition1d];
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

        public bool TryRemoveBlockAt(Vector3 globalPosition)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                return false;
            }

            int localPosition1d = ConvertGlobalPositionToLocal1D(globalPosition);

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
            _Bounds = new Bounds(Position + Size.Divide(2), Size);
        }

        private int ConvertGlobalPositionToLocal1D(Vector3 position)
        {
            Vector3 localPosition = (position - Position).Abs();
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

        /// <summary>
        ///     Scans the block array and returns the highest index that is non-air
        /// </summary>
        /// <param name="blocks">Array of blocks to scan</param>
        /// <param name="startIndex"></param>
        /// <param name="strideSize">Number of indexes to jump each iteration</param>
        /// <param name="maxHeight">Maximum amount of iterations to stride</param>
        /// <returns></returns>
        public static int GetTopmostBlockIndex(Block[] blocks, int startIndex, int strideSize, int maxHeight)
        {
            int highestNonAirIndex = 0;

            for (int y = 0; y < maxHeight; y++)
            {
                int currentIndex = startIndex + (y * strideSize);

                if (currentIndex >= blocks.Length)
                {
                    break;
                }

                if (blocks[currentIndex].Id == BlockController.BLOCK_EMPTY_ID)
                {
                    continue;
                }

                highestNonAirIndex = currentIndex;
            }

            return highestNonAirIndex;
        }

        #endregion


        #region EVENTS

        // todo chunk load failed event

        public event EventHandler<ChunkChangedEventArgs> BlocksChanged;
        public event EventHandler<ChunkChangedEventArgs> MeshChanged;
        public event EventHandler<ChunkChangedEventArgs> DeactivationCallback;

        protected virtual void OnBlocksChanged(object sender, ChunkChangedEventArgs args)
        {
            BlocksChanged?.Invoke(sender, args);
        }

        protected virtual void OnMeshChanged(object sender, ChunkChangedEventArgs args)
        {
            MeshChanged?.Invoke(sender, args);
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
