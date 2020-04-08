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

        private Mesh _Mesh;
        private object _JobIdentity;
        private bool _UpdateRequested;

        public bool Meshed { get; private set; }
        public bool Meshing { get; private set; }

        #endregion

        #region SERIALIZED MEMBERS

        [SerializeField]
        private MeshFilter MeshFilter;

        [SerializeField]
        private ChunkBlocksController BlocksController;

        [SerializeField]
        private ChunkTerrainController TerrainController;

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

            _Mesh = new Mesh();
            MeshFilter.sharedMesh = _Mesh;
            Meshed = Meshing = false;

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

        public async Task FrameUpdate()
        {
            if (Meshing
                || !_UpdateRequested
                || (BlocksController.PendingBlockActions > 0)
                || (TerrainController.CurrentStep != GenerationData.GenerationStep.Complete)
                || !WorldController.Current.ReadyForGeneration
                || (WorldController.Current.AggregateNeighborsStep(WydMath.ToInt(_Volume.MinPoint))
                    < GenerationData.GenerationStep.Complete))
            {
                return;
            }

            await BeginGeneratingMesh();
        }

        #region DE/ACTIVATION

        private void ClearInternalData()
        {
            if (_Mesh != default)
            {
                _Mesh.Clear();
            }

            _JobIdentity = null;
            Meshing = Meshed = false;

#if UNITY_EDITOR

            TimesMeshed = VertexCount = TrianglesCount = UVsCount = 0;

#endif
        }

        #endregion

        public void FlagForUpdate()
        {
            if (!_UpdateRequested)
            {
                _UpdateRequested = true;
            }
        }

        private async Task BeginGeneratingMesh()
        {
            if (Meshing)
            {
                return;
            }

            ChunkMeshingJob asyncJob = new ChunkMeshingJob(new GenerationData(_Volume, BlocksController.Blocks), true);

            if (!await QueueAsyncJob(asyncJob))
            {
                return;
            }

            SystemController.Current.JobFinished += OnJobFinished;
            Meshed = _UpdateRequested = false;
            Meshing = true;
        }

        private void ApplyMesh(ChunkMeshingJob chunkMeshingJob)
        {
            chunkMeshingJob.SetMesh(ref _Mesh);
            OnMeshChanged(this, new ChunkChangedEventArgs(_Volume, Enumerable.Empty<int3>()));
        }

        private async Task<bool> QueueAsyncJob(AsyncJob asyncJob)
        {
            object identity = await SystemController.Current.QueueAsyncJob(asyncJob);

            if (identity == null)
            {
                return false;
            }

            _JobIdentity = identity;
            SystemController.Current.JobFinished += OnJobFinished;

            return true;
        }

        #region EVENTS

        public event ChunkChangedEventHandler MeshChanged;

        private void OnMeshChanged(object sender, ChunkChangedEventArgs args)
        {
            MeshChanged?.Invoke(sender, args);
        }

        private void OnJobFinished(object sender, AsyncJobEventArgs args)
        {
            if ((args.AsyncJob.Identity != _JobIdentity) || !(args.AsyncJob is ChunkMeshingJob chunkMeshingJob))
            {
                return;
            }

            MainThreadActionsController.Current.PushAction(new MainThreadAction(default,
                () => ApplyMesh(chunkMeshingJob)));
            SystemController.Current.JobFinished -= OnJobFinished;
            _JobIdentity = null;
            Meshing = false;
            Meshed = true;
        }

        #endregion
    }
}
