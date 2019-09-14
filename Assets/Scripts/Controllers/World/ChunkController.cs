#region

using System;
using System.Collections.Generic;
using System.Linq;
using Collections;
using Controllers.State;
using Game;
using Game.Entities;
using Game.World.Blocks;
using Game.World.Chunks;
using Logging;
using NLog;
using Threading.ThreadedItems;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Controllers.World
{
    public enum ThreadingMode
    {
        Single = 0,
        Multi = 1
    }

    public class ChunkController : MonoBehaviour
    {
        private static bool _computeShaderGlobalsSet;

        private static readonly ObjectCache<ChunkBuildingThreadedItem> ChunkBuildersCache =
            new ObjectCache<ChunkBuildingThreadedItem>(null, null, true);

        private static readonly ObjectCache<ChunkMeshingThreadedItem> ChunkMeshersCache =
            new ObjectCache<ChunkMeshingThreadedItem>(null, null, true);


        public static readonly Vector3Int Size = new Vector3Int(16, 256, 16);
        public static readonly int YIndexStep = Size.x * Size.z;

        public static readonly IEnumerable<Vector3> CardinalDirectionsVector3 = new[]
        {
            Vector3.forward,
            Vector3.right,
            Vector3.back,
            Vector3.left
        };

        public static FixedConcurrentQueue<TimeSpan> BuildTimes;
        public static FixedConcurrentQueue<TimeSpan> MeshTimes;

        private Bounds _Bounds;
        private Action _PendingAction;
        private Transform _SelfTransform;
        private IEntity _CurrentLoader;
        private Block[] _Blocks;
        private Mesh _Mesh;
        private object _BuildingIdentity;
        private object _MeshingIdentity;
        private bool _UpdateInternalSettingsOnNextFrame;
        private bool _Visible;
        private bool _RenderShadows;

        public ComputeShader GenerationComputeShader;
        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public bool Built;
        public bool Building;
        public bool Meshed;
        public bool Meshing;
        public bool UpdateMesh;
        public bool AggressiveFaceMerging;

        public Vector3 Position { get; private set; }

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

        public bool Active => gameObject.activeSelf;

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

        // todo chunk load failed event

        public event EventHandler<ChunkChangedEventArgs> BlocksChanged;
        public event EventHandler<ChunkChangedEventArgs> MeshChanged;
        public event EventHandler<ChunkChangedEventArgs> DeactivationCallback;

        private void Awake()
        {
            if (!_computeShaderGlobalsSet)
            {
                GenerationComputeShader.SetInt("_NoiseSeed", WorldController.Current.WorldGenerationSettings.Seed);
                GenerationComputeShader.SetVector("_MaximumSize", new Vector4(Size.x, Size.y, Size.z, 0f));

                _computeShaderGlobalsSet = true;
            }

            _SelfTransform = transform;
            Position = _SelfTransform.position;
            UpdateBounds();
            _Blocks = new Block[Size.Product()];
            _Mesh = new Mesh();

            MeshFilter.sharedMesh = _Mesh;
            MeshRenderer.material.SetTexture(TextureController.Current.MainTex,
                TextureController.Current.TerrainTexture);
            Built = Building = Meshed = Meshing = UpdateMesh = false;
            AggressiveFaceMerging = true;

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
            if (BuildTimes == default)
            {
                BuildTimes =
                    new FixedConcurrentQueue<TimeSpan>(OptionsController.Current.MaximumChunkLoadTimeBufferSize);
            }

            if (MeshTimes == default)
            {
                MeshTimes = new FixedConcurrentQueue<TimeSpan>(OptionsController.Current
                    .MaximumChunkLoadTimeBufferSize);
            }

            _UpdateInternalSettingsOnNextFrame = true;

            if (_CurrentLoader == default)
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Chunk at position {Position} has been initialized without a loader. This is possibly an error.");
            }
        }

        private void Update()
        {
            if (WorldController.Current.IsOnBorrowedUpdateTime())
            {
                return;
            }

            if (_UpdateInternalSettingsOnNextFrame)
            {
                OnCurrentLoaderChangedChunk(this, _CurrentLoader.CurrentChunk);
            }

            GenerationStateCheckAndStart();

            if (_PendingAction == default)
            {
                return;
            }

            _PendingAction?.Invoke();
            _PendingAction = default;
        }

        private void OnDestroy()
        {
            Destroy(_Mesh);
        }


        #region ACTIVATION STATE

        public void Activate(Vector3 position)
        {
            _SelfTransform.position = Position = position;
            UpdateBounds();
            Built = Building = Meshed = Meshing = UpdateMesh = false;
            _UpdateInternalSettingsOnNextFrame = true;
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
            }

            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        public void AssignLoader(IEntity loader)
        {
            _CurrentLoader = loader;
            _CurrentLoader.ChunkPositionChanged += OnCurrentLoaderChangedChunk;
        }

        #endregion


        #region CHUNK GENERATION

        private void GenerationStateCheckAndStart()
        {
            CheckStateAndStartBuilding();
            CheckStateAndStartMeshing();
        }

        private void CheckStateAndStartBuilding()
        {
            if (Built || Building)
            {
                return;
            }

            ThreadedItem threadedItem = GetChunkBuildingThreadedItem(0.01f, OptionsController.Current.GPUAcceleration);

            if (threadedItem == default)
            {
                EventLog.Logger.Log(LogLevel.Error, $"Failed to retrieve building item for chunk at {Position}.");
                return;
            }

            // do a full update of state booleans
            Built = Meshed = Meshing = UpdateMesh = false;
            Building = true;

            GameController.ThreadedExecutionQueue.ThreadedItemFinished += OnThreadedQueueFinishedItem;
            _BuildingIdentity = GameController.ThreadedExecutionQueue.QueueThreadedItem(threadedItem);
        }

        private void CheckStateAndStartMeshing()
        {
            if (!Built
                || Meshing
                || (!UpdateMesh && Meshed)
                || !Visible
                || !WorldController.Current.AreNeighborsBuilt(Position))
            {
                return;
            }

            ThreadedItem threadedItem = GetChunkMeshingThreadedItem();

            if (threadedItem == default)
            {
                EventLog.Logger.Log(LogLevel.Error, $"Failed to retrieve meshing item for chunk at {Position}.");
                return;
            }

            // do a full update of state booleans
            Meshed = UpdateMesh = false;
            Meshing = true;

            GameController.ThreadedExecutionQueue.ThreadedItemFinished += OnThreadedQueueFinishedItem;
            _MeshingIdentity = GameController.ThreadedExecutionQueue.QueueThreadedItem(threadedItem);
        }

        private void OnThreadedQueueFinishedItem(object sender, ThreadedItemFinishedEventArgs args)
        {
            if (args.ThreadedItem.Identity == _BuildingIdentity)
            {
                Building = false;
                Built = UpdateMesh = true;
                GameController.ThreadedExecutionQueue.ThreadedItemFinished -= OnThreadedQueueFinishedItem;

                BuildTimes.Enqueue(args.ThreadedItem.ExecutionTime);

                OnBlocksChanged(new ChunkChangedEventArgs(_Bounds, CardinalDirectionsVector3));
            }
            else if (args.ThreadedItem.Identity == _MeshingIdentity)
            {
                Meshing = false;
                Meshed = true;
                GameController.ThreadedExecutionQueue.ThreadedItemFinished -= OnThreadedQueueFinishedItem;

                // Safely apply mesh when there is free frame time
                _PendingAction = () => ApplyMesh((ChunkMeshingThreadedItem) args.ThreadedItem);

                MeshTimes.Enqueue(args.ThreadedItem.ExecutionTime);

                OnMeshChanged(new ChunkChangedEventArgs(_Bounds, Enumerable.Empty<Vector3>()));
            }
        }

        private ThreadedItem GetChunkBuildingThreadedItem(float frequency = 0.01f, bool gpuAcceleration = false)
        {
            ChunkBuildingThreadedItem threadedItem = ChunkBuildersCache.RetrieveItem();

            if (gpuAcceleration)
            {
                ComputeBuffer noiseValuesBuffer = new ComputeBuffer(Size.Product(), 4);
                int kernel = GenerationComputeShader.FindKernel("CSMain");
                GenerationComputeShader.SetVector("_Offset", Position);
                GenerationComputeShader.SetFloat("_Frequency", 0.01f);
                GenerationComputeShader.SetBuffer(kernel, "Result", noiseValuesBuffer);
                // 256 is the value set in the shader's [numthreads(--> 256 <--, 1, 1)]
                GenerationComputeShader.Dispatch(kernel, Size.Product() / 256, 1, 1);

                threadedItem.Set(Position, _Blocks, frequency, gpuAcceleration, noiseValuesBuffer);
            }
            else
            {
                threadedItem.Set(Position, _Blocks, frequency);
            }

            return threadedItem;
        }

        private ThreadedItem GetChunkMeshingThreadedItem()
        {
            ChunkMeshingThreadedItem threadedItem = ChunkMeshersCache.RetrieveItem();
            threadedItem.Set(Position, _Blocks, AggressiveFaceMerging, Meshed);

            return threadedItem;
        }

        private void ApplyMesh(ChunkMeshingThreadedItem threadedItem)
        {
            threadedItem.SetMesh(ref _Mesh);
        }

        #endregion


        #region MISC

        private void UpdateBounds()
        {
            _Bounds = new Bounds(Position + Size.Divide(2), Size);
        }

        private int ConvertGlobalPositionToLocal1D(Vector3 position)
        {
            Vector3 localPosition = (position - Position).Abs();
            return localPosition.To1D(Size);
        }

        public Block GetBlockAt(Vector3 globalPosition)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(globalPosition), globalPosition,
                    $"Given position `{globalPosition}` exists outside of local bounds.");
            }

            int localPosition1d = ConvertGlobalPositionToLocal1D(globalPosition);

            if (!Built)
            {
                throw new Exception("Requested block present in chunk that hasn't finished building.'");
            }

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
            UpdateMesh = true;

            OnBlocksChanged(new ChunkChangedEventArgs(_Bounds, DetermineDirectionsForNeighborUpdate(globalPosition)));
        }

        public bool TryPlaceBlockAt(Vector3 globalPosition, ushort id)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                return false;
            }

            int localPosition1d = ConvertGlobalPositionToLocal1D(globalPosition);

            _Blocks[localPosition1d].Initialise(id);
            UpdateMesh = true;

            OnBlocksChanged(new ChunkChangedEventArgs(_Bounds, DetermineDirectionsForNeighborUpdate(globalPosition)));
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
            UpdateMesh = true;

            OnBlocksChanged(new ChunkChangedEventArgs(_Bounds, DetermineDirectionsForNeighborUpdate(globalPosition)));
        }

        public bool TryRemoveBlockAt(Vector3 globalPosition)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                return false;
            }

            int localPosition1d = ConvertGlobalPositionToLocal1D(globalPosition);

            _Blocks[localPosition1d].Initialise(BlockController.BLOCK_EMPTY_ID);
            UpdateMesh = true;

            OnBlocksChanged(new ChunkChangedEventArgs(_Bounds, DetermineDirectionsForNeighborUpdate(globalPosition)));
            return true;
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
                    yield return Vector3.forward;
                    yield return Vector3.right;
                }
            }
            else if (!isInTopLeftHalf && !isInBottomRightHalf)
            {
                if (isInTopRightHalf)
                {
                    yield return Vector3.back;
                    yield return Vector3.right;
                }
                else if (isInBottomLeftHalf)
                {
                    yield return Vector3.forward;
                    yield return Vector3.left;
                }
            }
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
                DeactivationCallback?.Invoke(this, new ChunkChangedEventArgs(_Bounds, CardinalDirectionsVector3));
                return;
            }

            Visible = IsWithinRenderDistance(difference);
            RenderShadows = IsWithinDrawShadowsDistance(difference);
            _UpdateInternalSettingsOnNextFrame = false;
        }

        private static bool IsWithinLoaderRange(Vector3 difference)
        {
            return difference.AllLessThanOrEqual(Size
                                                 * (WorldController.Current.WorldGenerationSettings.Radius
                                                    + OptionsController.Current.PreLoadChunkDistance));
        }

        private static bool IsWithinRenderDistance(Vector3 difference)
        {
            return difference.AllLessThanOrEqual(Size * WorldController.Current.WorldGenerationSettings.Radius);
        }

        private static bool IsWithinDrawShadowsDistance(Vector3 difference)
        {
            return (OptionsController.Current.ShadowDistance == 0)
                   || difference.AllLessThanOrEqual(Size * OptionsController.Current.ShadowDistance);
        }

        #endregion

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

        protected virtual void OnBlocksChanged(ChunkChangedEventArgs args)
        {
            BlocksChanged?.Invoke(this, args);
        }

        protected virtual void OnMeshChanged(ChunkChangedEventArgs args)
        {
            MeshChanged?.Invoke(this, args);
        }
    }
}
