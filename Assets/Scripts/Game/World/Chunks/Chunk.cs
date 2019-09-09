#region

using System;
using Collections;
using Controllers.Entity;
using Controllers.Game;
using Controllers.World;
using Game.Entity;
using Game.World.Blocks;
using Logging;
using NLog;
using Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Game.World.Chunks
{
    public enum ThreadingMode
    {
        Single = 0,
        Multi = 1
    }

    public class Chunk : MonoBehaviour, IEntityChunkChangedSubscriber
    {
        private static readonly int Offset = Shader.PropertyToID("_Offset");

        private static readonly ObjectCache<ChunkBuildingThreadedItem> ChunkBuildersCache =
            new ObjectCache<ChunkBuildingThreadedItem>(null, null, true);

        private static readonly ObjectCache<ChunkMeshingThreadedItem> ChunkMeshersCache =
            new ObjectCache<ChunkMeshingThreadedItem>(null, null, true);

        private static ThreadedQueue _threadedExecutionQueue;

        public static readonly Vector3Int Size = new Vector3Int(16, 256, 16);
        public static FixedConcurrentQueue<TimeSpan> BuildTimes;
        public static FixedConcurrentQueue<TimeSpan> MeshTimes;

        private Bounds _Bounds;
        private Action _PendingAction;
        private Transform _SelfTransform;
        private RenderTexture _BuiltTexture;
        private Block[] _Blocks;
        private Mesh _Mesh;
        private object _BuildingIdentity;
        private object _MeshingIdentity;
        private bool _OnBorrowedUpdateTime;
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

        public bool PrimaryLoaderChangedChunk { get; set; }

        public event EventHandler<Bounds> BlocksChanged;
        public event EventHandler<Bounds> MeshChanged;
        public event EventHandler<Bounds> DeactivationCallback;

        private void Awake()
        {
            _SelfTransform = transform;
            Position = _SelfTransform.position;
            UpdateBounds();
            _Blocks = new Block[Size.Product()];
            _OnBorrowedUpdateTime = Built = Building = Meshed = Meshing = UpdateMesh = false;
            _Mesh = new Mesh();

            GenerationComputeShader.SetVector("_MaximumSize", new Vector4(Size.x, Size.y, Size.z));
            GenerationComputeShader.SetVector("_Offset", Position);
            MeshFilter.sharedMesh = _Mesh;
            MeshRenderer.material.SetTexture(TextureController.Current.MainTex,
                TextureController.Current.TerrainTexture);
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
            if (_threadedExecutionQueue == default)
            {
                // init ThreadedQueue with # of threads matching 1/2 of logical processors
                _threadedExecutionQueue = new ThreadedQueue(200, () => OptionsController.Current.ThreadingMode,
                    Environment.ProcessorCount / 2);
                _threadedExecutionQueue.Start();
            }

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

            PlayerController.Current.RegisterEntityChangedSubscriber(this);
            CheckInternalSettings(PlayerController.Current.CurrentChunk);
        }

        private void Update()
        {
            _OnBorrowedUpdateTime = WorldController.Current.IsOnBorrowedUpdateTime();

            if (_OnBorrowedUpdateTime)
            {
                return;
            }

            if (PrimaryLoaderChangedChunk)
            {
                CheckInternalSettings(PlayerController.Current.CurrentChunk);
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

        private void OnApplicationQuit()
        {
            // Deallocate and destroy ALL NativeCollection / disposable objects
            _threadedExecutionQueue.Abort();
        }


        #region ACTIVATION STATE

        public void Activate(Vector3 position)
        {
            _SelfTransform.position = Position = position;
            GenerationComputeShader.SetVector("_Offset", Position);
            UpdateBounds();
            Built = Building = Meshed = Meshing = UpdateMesh = false;
            CheckInternalSettings(PlayerController.Current.CurrentChunk);
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            if (_Mesh != default)
            {
                _Mesh.Clear();
            }

            StopAllCoroutines();
            gameObject.SetActive(false);
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

            // will enable this when I can get the time to get it working
            // ComputeBuffer buffer = new ComputeBuffer(Size.Product(), 4);
            // float[] output = new float[Size.Product()];
            // int kernel = GenerationComputeShader.FindKernel("CSMain");
            // GenerationComputeShader.SetBuffer(kernel, "Result", buffer);
            // GenerationComputeShader.Dispatch(kernel, Size.Product() / 256, 1, 1);
            // buffer.GetData(output);
            // buffer.Release();

            ThreadedItem threadedItem = GetChunkBuildingThreadedItem();

            if (threadedItem == default)
            {
                EventLog.Logger.Log(LogLevel.Error, $"Failed to retrieve building item for chunk at {Position}.");
                return;
            }

            // do a full update of state booleans
            Built = Meshed = Meshing = UpdateMesh = false;
            Building = true;

            _threadedExecutionQueue.ThreadedItemFinished += OnThreadedQueueFinishedItem;
            _BuildingIdentity = _threadedExecutionQueue.QueueThreadedItem(threadedItem);
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

            _threadedExecutionQueue.ThreadedItemFinished += OnThreadedQueueFinishedItem;
            _MeshingIdentity = _threadedExecutionQueue.QueueThreadedItem(threadedItem);
        }

        private void OnThreadedQueueFinishedItem(object sender, ThreadedItemFinishedEventArgs args)
        {
            if (args.ThreadedItem.Identity == _BuildingIdentity)
            {
                Building = false;
                Built = UpdateMesh = true;
                _threadedExecutionQueue.ThreadedItemFinished -= OnThreadedQueueFinishedItem;

                BuildTimes.Enqueue(args.ThreadedItem.ExecutionTime);
                
                OnBlocksChanged();
            }
            else if (args.ThreadedItem.Identity == _MeshingIdentity)
            {
                Meshing = false;
                Meshed = true;
                _threadedExecutionQueue.ThreadedItemFinished -= OnThreadedQueueFinishedItem;

                // Safely apply mesh when there is free frame time
                _PendingAction = () => ApplyMesh((ChunkMeshingThreadedItem) args.ThreadedItem);

                MeshTimes.Enqueue(args.ThreadedItem.ExecutionTime);
                
                OnMeshChanged();
            }
        }

        private ThreadedItem GetChunkBuildingThreadedItem(bool memoryNegligent = false, float[] noiseValues = null)
        {
            ChunkBuildingThreadedItem threadedItem = ChunkBuildersCache.RetrieveItem();
            threadedItem.Set(Position, _Blocks, memoryNegligent, noiseValues);

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
            _Bounds = new Bounds(Position +  Size.Divide(2), Size);
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

            OnBlocksChanged();
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

            OnBlocksChanged();
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

            OnBlocksChanged();
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

            OnBlocksChanged();
            return true;
        }
        
        private void CheckInternalSettings(Vector3 loaderChunkPosition)
        {
            if (Position == loaderChunkPosition)
            {
                return;
            }

            Vector3 difference = (Position - loaderChunkPosition).Abs();

            if (!IsWithinLoaderRange(difference))
            {
                DeactivationCallback?.Invoke(this, _Bounds);
                return;
            }

            Visible = IsWithinRenderDistance(difference);
            RenderShadows = IsWithinDrawShadowsDistance(difference);
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

        protected virtual void OnBlocksChanged()
        {
            BlocksChanged?.Invoke(this, _Bounds);
        }

        protected virtual void OnMeshChanged()
        {
            MeshChanged?.Invoke(this, _Bounds);
        }
    }
}
