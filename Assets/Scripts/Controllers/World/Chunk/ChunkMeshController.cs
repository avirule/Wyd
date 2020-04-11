#region

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.System;
using Wyd.Game.World.Chunks;
using Wyd.Game.World.Chunks.Events;
using Wyd.System;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    public class ChunkMeshController : ActivationStateChunkController, IPerFrameUpdate
    {
        #region INSTANCE MEMBERS

        private object _MeshStateHandle;
        private CancellationTokenSource _CancellationTokenSource;
        private Mesh _Mesh;

        public MeshState MeshState
        {
            get
            {
                MeshState tmp;

                lock (_MeshStateHandle)
                {
                    tmp = _MeshState;
                }

                return tmp;
            }
            private set
            {
                lock (_MeshStateHandle)
                {
                    _MeshState = value;
                }
            }
        }

        #endregion

        #region SERIALIZED MEMBERS

        [SerializeField]
        private MeshFilter MeshFilter;

        [SerializeField]
        private ChunkBlocksController BlocksController;

        [SerializeField]
        private ChunkTerrainController TerrainController;

        [SerializeField]
        private MeshState _MeshState;

#if UNITY_EDITOR

        [SerializeField]
        [ReadOnlyInspectorField]
        private long VertexCount;

        [SerializeField]
        [ReadOnlyInspectorField]
        private long TrianglesCount;

        [SerializeField]
        [ReadOnlyInspectorField]
        private long UVsCount;

#endif

        /// <summary>
        ///     Total numbers of times chunk has been
        ///     meshed, persisting through de/activation.
        /// </summary>
        [SerializeField]
        [ReadOnlyInspectorField]
        private long TotalTimesMeshed;

        /// <summary>
        ///     Number of times chunk has been meshed,
        ///     resetting every de/activation.
        /// </summary>
        [SerializeField]
        [ReadOnlyInspectorField]
        private long TimesMeshed;

        #endregion

        protected override void Awake()
        {
            base.Awake();

            _MeshStateHandle = new object();
            _Mesh = new Mesh();
            MeshFilter.sharedMesh = _Mesh;
            MeshState = 0;

#if UNITY_EDITOR

            // update debug data when mesh changed
            MeshChanged += (sender, args) =>
            {
                VertexCount = _Mesh.vertices.Length;
                TrianglesCount = _Mesh.triangles.Length;
                UVsCount = _Mesh.uv.Length;
                TotalTimesMeshed += 1;
                TimesMeshed += 1;
            };

#endif
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            PerFrameUpdateController.Current.RegisterPerFrameUpdater(50, this);
            ClearInternalData();

            _CancellationTokenSource = new CancellationTokenSource();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            PerFrameUpdateController.Current.DeregisterPerFrameUpdater(50, this);
            ClearInternalData();

            _CancellationTokenSource.Cancel();
        }

        public void FrameUpdate()
        {
            if (MeshState.HasFlag(MeshState.Meshing)
                || !MeshState.HasFlag(MeshState.UpdateRequested)
                || (BlocksController.PendingBlockActions > 0)
                || (TerrainController.TerrainStep != TerrainStep.Complete)
                || !WorldController.Current.ReadyForGeneration
                || (WorldController.Current.AggregateNeighborsStep(OriginPoint) < TerrainStep.Complete))
            {
                return;
            }

            BeginGeneratingMesh();
        }

        #region DE/ACTIVATION

        private void ClearInternalData()
        {
            if (_Mesh != default)
            {
                _Mesh.Clear();
            }

            MeshState = 0;

#if UNITY_EDITOR

            TimesMeshed = VertexCount = TrianglesCount = UVsCount = 0;

#endif
        }

        #endregion

        public void FlagForUpdate()
        {
            if (!MeshState.HasFlag(MeshState.UpdateRequested))
            {
                MeshState |= MeshState.UpdateRequested;
            }
        }

        private void BeginGeneratingMesh()
        {
            _CancellationTokenSource?.Cancel();
            _CancellationTokenSource = new CancellationTokenSource();

            ChunkMeshingJob asyncJob = new ChunkMeshingJob(_CancellationTokenSource.Token, OriginPoint, BlocksController.Blocks, true);

            QueueAsyncJob(asyncJob);
        }

        private void ApplyMesh(ChunkMeshingJob chunkMeshingJob)
        {
            chunkMeshingJob.SetMesh(ref _Mesh);
            MeshState = (MeshState | MeshState.Meshed) & ~MeshState.Meshing;
            OnMeshChanged(this, new ChunkChangedEventArgs(OriginPoint, Enumerable.Empty<int3>()));
        }

        private void QueueAsyncJob(AsyncJob asyncJob)
        {
            asyncJob.WorkFinished += OnJobFinished;

            Task.Run(async () => await AsyncJobScheduler.QueueAsyncJob(asyncJob));

            MeshState = MeshState.Meshing;
        }

        #region EVENTS

        public event ChunkChangedEventHandler MeshChanged;

        private void OnMeshChanged(object sender, ChunkChangedEventArgs args)
        {
            MeshChanged?.Invoke(sender, args);
        }

        private void OnJobFinished(object sender, AsyncJobEventArgs args)
        {
            if (!(sender is ChunkMeshingJob meshingJob))
            {
                return;
            }

            meshingJob.WorkFinished -= OnJobFinished;
            MainThreadActionsController.Current.PushAction(new MainThreadAction(default,
                () => ApplyMesh(meshingJob)));
        }

        #endregion
    }
}
