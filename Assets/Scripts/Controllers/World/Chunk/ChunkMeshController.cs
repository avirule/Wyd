#region

using System.Linq;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.System;
using Wyd.Game.World;
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

        private Mesh _Mesh;

        public GenerationData.MeshState MeshState
        {
            get
            {
                GenerationData.MeshState tmp;

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
        private GenerationData.MeshState _MeshState;

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
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            PerFrameUpdateController.Current.DeregisterPerFrameUpdater(50, this);
            ClearInternalData();
        }

        public void FrameUpdate()
        {
            if (MeshState.HasFlag(GenerationData.MeshState.Meshing)
                || !MeshState.HasFlag(GenerationData.MeshState.UpdateRequested)
                || (BlocksController.PendingBlockActions > 0)
                || (TerrainController.CurrentStep != GenerationData.GenerationStep.Complete)
                || !WorldController.Current.ReadyForGeneration
                || (WorldController.Current.AggregateNeighborsStep(WydMath.ToInt(_Volume.MinPoint))
                    < GenerationData.GenerationStep.Complete))
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
            if (!MeshState.HasFlag(GenerationData.MeshState.UpdateRequested))
            {
                MeshState |= GenerationData.MeshState.UpdateRequested;
            }
        }

        private void BeginGeneratingMesh()
        {
            ChunkMeshingJob asyncJob = new ChunkMeshingJob(new GenerationData(_Volume, BlocksController.Blocks), true);

            QueueAsyncJob(asyncJob);
        }

        private void ApplyMesh(ChunkMeshingJob chunkMeshingJob)
        {
            chunkMeshingJob.SetMesh(ref _Mesh);
            OnMeshChanged(this, new ChunkChangedEventArgs(_Volume, Enumerable.Empty<int3>()));
        }

        private void QueueAsyncJob(AsyncJob asyncJob)
        {
            asyncJob.WorkFinished += OnJobFinished;

            Task.Run(async () => await JobScheduler.QueueAsyncJob(asyncJob));

            MeshState = GenerationData.MeshState.Meshing;
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

            MeshState = (MeshState | GenerationData.MeshState.Meshed) & ~GenerationData.MeshState.Meshing;
        }

        #endregion
    }
}
