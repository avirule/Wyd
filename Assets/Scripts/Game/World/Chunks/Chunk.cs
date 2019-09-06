#region

using System;
using System.Collections.Concurrent;
using Collections;
using Controllers.Entity;
using Controllers.Game;
using Controllers.World;
using Game.Entity;
using Game.World.Blocks;
using Threading;
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
        private static readonly ObjectCache<ChunkBuildingThreadedItem> ChunkBuildersCache =
            new ObjectCache<ChunkBuildingThreadedItem>(null, null, true);

        private static readonly ObjectCache<ChunkMeshingThreadedItem> ChunkMeshersCache =
            new ObjectCache<ChunkMeshingThreadedItem>(null, null, true);

        private static ThreadedQueue _threadedExecutionQueue;

        public static readonly Vector3Int Size = new Vector3Int(16, 32, 16);
        public static FixedConcurrentQueue<TimeSpan> BuildTimes;
        public static FixedConcurrentQueue<TimeSpan> MeshTimes;

        private ComputeShader _BuilderShader;
        private int _BuilderShaderKernelHandle;
        private RenderTexture _Blocks3DTexture;
        private Vector3 _Position;
        private Block[] _Blocks;
        private Mesh _Mesh;
        private ConcurrentQueue<Action> _AsynchronousCoroutineQueue;
        private object _BuildingIdentity;
        private object _MeshingIdentity;
        private bool _OnBorrowedUpdateTime;
        private bool _Visible;
        private bool _RenderShadows;

        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public bool Built;
        public bool Building;
        public bool Meshed;
        public bool Meshing;
        public bool PendingMeshUpdate;

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

        public event EventHandler<Vector3> DeactivationCallback;

        public Vector3 Position
        {
            get => _Position;
            set
            {
                _Position = value;
                transform.position = _Position;
            }
        }

        private void Awake()
        {
            // create render texture for blocks computing
            _BuilderShader = Resources.Load<ComputeShader>(@"Graphics\Shaders\SimplexNoise3D");
            _BuilderShaderKernelHandle = _BuilderShader.FindKernel("CSMain");
            _Blocks3DTexture = new RenderTexture(Size.x, Size.y, Size.z, RenderTextureFormat.ARGB32)
            {
                enableRandomWrite = true
            };
            _Blocks3DTexture.Create();
            
            _Position = transform.position;
            _Blocks = new Block[Size.Product()];
            _OnBorrowedUpdateTime = Built = Building = Meshed = Meshing = PendingMeshUpdate = false;
            _Mesh = new Mesh();
            _AsynchronousCoroutineQueue = new ConcurrentQueue<Action>();

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
            if (_threadedExecutionQueue == default)
            {
                _threadedExecutionQueue = new ThreadedQueue(200, () => OptionsController.Current.ThreadingMode, Environment.ProcessorCount);
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
            CheckInternalsAgainstLoaderPosition(PlayerController.Current.CurrentChunk);
        }

        private void Update()
        {
            _OnBorrowedUpdateTime = WorldController.Current.IsOnBorrowedUpdateTime();

            if (PrimaryLoaderChangedChunk)
            {
                CheckInternalsAgainstLoaderPosition(PlayerController.Current.CurrentChunk);
            }

            if (!_OnBorrowedUpdateTime)
            {
                GenerationCheckAndStart();
            }

            while (!_OnBorrowedUpdateTime && _AsynchronousCoroutineQueue.TryDequeue(out Action action))
            {
                action?.Invoke();
            }
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

        public void Activate(Vector3 position = default)
        {
            Position = position;
            Built = Building = Meshed = Meshing = PendingMeshUpdate = false;
            CheckInternalsAgainstLoaderPosition(PlayerController.Current.CurrentChunk);
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

        private void GenerationCheckAndStart()
        {
            CheckBuildingOrStart();
            CheckMeshingOrStart();
        }

        private void CheckBuildingOrStart()
        {
            if (!Built && !Building)
            {
                ThreadedItem threadedItem = GetChunkBuildingThreadedItem();

                if (threadedItem == default)
                {
                    return;
                }

                _threadedExecutionQueue.ThreadedItemFinished += OnThreadedQueueFinishedItem;
                _BuildingIdentity = _threadedExecutionQueue.QueueThreadedItem(threadedItem);
                
                
                
                //     compute shader building
                _BuilderShader.SetTexture(_BuilderShaderKernelHandle, "Result", _Blocks3DTexture);
                _BuilderShader.Dispatch(_BuilderShaderKernelHandle, Size.x, Size.y, Size.z);
                
            }
        }

        private void CheckMeshingOrStart()
        {
            if (!Meshing && (PendingMeshUpdate || !Meshed) && Visible &&
                WorldController.Current.AreNeighborsBuilt(Position))
            {
                ThreadedItem threadedItem = GetChunkMeshingThreadedItem();

                if (threadedItem == default)
                {
                    return;
                }

                _threadedExecutionQueue.ThreadedItemFinished += OnThreadedQueueFinishedItem;
                _MeshingIdentity = _threadedExecutionQueue.QueueThreadedItem(threadedItem);
            }
        }

        private void OnThreadedQueueFinishedItem(object sender, ThreadedItemFinishedEventArgs args)
        {
            if (args.ThreadedItem.Identity == _BuildingIdentity)
            {
                Building = false;
                Built = PendingMeshUpdate = true;
                _threadedExecutionQueue.ThreadedItemFinished -= OnThreadedQueueFinishedItem;

                BuildTimes.Enqueue(args.ThreadedItem.ExecutionTime);
            }
            else if (args.ThreadedItem.Identity == _MeshingIdentity)
            {
                Meshing = false;
                Meshed = true;
                _threadedExecutionQueue.ThreadedItemFinished -= OnThreadedQueueFinishedItem;

                // Safely apply mesh when there is free frame time
                _AsynchronousCoroutineQueue.Enqueue(() => ApplyMesh((ChunkMeshingThreadedItem) args.ThreadedItem));

                MeshTimes.Enqueue(args.ThreadedItem.ExecutionTime);
            }
        }

        private ThreadedItem GetChunkBuildingThreadedItem()
        {
            Built = false;
            Building = true;

            ChunkBuildingThreadedItem threadedItem = ChunkBuildersCache.RetrieveItem();
            threadedItem.Set(Position, _Blocks);

            return threadedItem;
        }

        private ThreadedItem GetChunkMeshingThreadedItem()
        {
            if (!Built)
            {
                return default;
            }

            Meshed = PendingMeshUpdate = false;
            Meshing = true;

            ChunkMeshingThreadedItem threadedItem = ChunkMeshersCache.RetrieveItem();
            threadedItem.Set(Position, _Blocks, true);

            return threadedItem;
        }

        private void ApplyMesh(ChunkMeshingThreadedItem threadedItem)
        {
            threadedItem.SetMesh(ref _Mesh);
        }

        #endregion


        #region MISC

        private int ConvertGlobalPositionToLocal1D(Vector3 position)
        {
            Vector3 localPosition = (position - Position).Abs();
            return localPosition.To1D(Size);
        }

        public Block GetBlockAt(Vector3 position)
        {
            // localize position value
            int localPosition1d = ConvertGlobalPositionToLocal1D(position);

            if ((localPosition1d < 0) || (_Blocks.Length <= localPosition1d))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position,
                    "Given position exists outside of local bounds.");
            }

            return _Blocks[localPosition1d];
        }

        public bool BlockExistsAt(Vector3 position)
        {
            // localize position value
            int localPosition1d = ConvertGlobalPositionToLocal1D(position);

            if (_Blocks.Length <= localPosition1d)
            {
                throw new ArgumentOutOfRangeException(nameof(position), position,
                    "Given position exists outside of local bounds.");
            }

            return _Blocks[localPosition1d].Id != BlockController.BLOCK_EMPTY_ID;
        }

        private void CheckInternalsAgainstLoaderPosition(Vector3 loaderChunkPosition)
        {
            if (Position == loaderChunkPosition)
            {
                return;
            }

            // chunk player is in should always be expensive / shadowed
            Vector3 difference = (Position - loaderChunkPosition).Abs();

            if (!IsWithinLoaderRange(difference))
            {
                DeactivationCallback?.Invoke(this, Position);
                return;
            }

            Visible = IsWithinRenderDistance(difference);
            RenderShadows = IsWithinDrawShadowsDistance(difference);
        }

        private static bool IsWithinLoaderRange(Vector3 difference)
        {
            int totalValidLoaderRadius =
                WorldController.Current.WorldGenerationSettings.Radius + OptionsController.Current.PreLoadChunkDistance;

            return (difference.x <= (totalValidLoaderRadius * Size.x)) &&
                   (difference.z <= (totalValidLoaderRadius * Size.z));
        }

        private static bool IsWithinRenderDistance(Vector3 difference)
        {
            return (difference.x <= (WorldController.Current.WorldGenerationSettings.Radius * Size.x)) &&
                   (difference.z <= (WorldController.Current.WorldGenerationSettings.Radius * Size.z));
        }

        private static bool IsWithinDrawShadowsDistance(Vector3 difference)
        {
            return (OptionsController.Current.ShadowDistance == 0) ||
                   ((difference.x <= (OptionsController.Current.ShadowDistance * Size.x)) &&
                    (difference.z <= (OptionsController.Current.ShadowDistance * Size.z)));
        }

        #endregion
    }
}