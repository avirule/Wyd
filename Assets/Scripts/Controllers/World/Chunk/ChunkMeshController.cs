#region

using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.System;
using Wyd.Game;
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

        [SerializeField]
        [ReadOnlyInspectorField]
        private long VertexCount;

        [SerializeField]
        [ReadOnlyInspectorField]
        private long TrianglesCount;

        [SerializeField]
        [ReadOnlyInspectorField]
        private long UVsCount;

        /// <summary>
        ///     Total numbers of times chunk has been
        ///     meshed, persisting through de/activation.
        /// </summary>
        [SerializeField]
        [ReadOnlyInspectorField]
        private int TotalTimesMeshed;

        /// <summary>
        ///     Number of times chunk has been meshed,
        ///     resetting every de/activation.
        /// </summary>
        [SerializeField]
        [ReadOnlyInspectorField]
        private int TimesMeshed;

        #endregion

        protected override void Awake()
        {
            base.Awake();

            _Mesh = new Mesh();
            MeshFilter.sharedMesh = _Mesh;
            Meshed = Meshing = false;

            // update debug data when mesh changed
            MeshChanged += (sender, args) =>
            {
                VertexCount = _Mesh.vertices.Length;
                TrianglesCount = _Mesh.triangles.Length;
                UVsCount = _Mesh.uv.Length;
                TotalTimesMeshed += 1;
                TimesMeshed += 1;
            };
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
            if (!_UpdateRequested
                || (BlocksController.QueuedBlockActions > 0)
                || (TerrainController.CurrentStep != GenerationData.GenerationStep.Complete)
                || !WorldController.Current.ReadyForGeneration
                || (WorldController.Current.AggregateNeighborsStep(WydMath.ToInt(_Bounds.MinPoint))
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

            VertexCount = TrianglesCount = UVsCount = TimesMeshed = 0;
            _JobIdentity = null;
            Meshing = Meshed = false;
        }

        #endregion

        public void FlagForUpdate()
        {
            if (!_UpdateRequested)
            {
                _UpdateRequested = true;
            }
        }

        private void BeginGeneratingMesh()
        {
            if (Meshing)
            {
                return;
            }

            ChunkMeshingJob chunkMeshingJob =
                new ChunkMeshingJob(new GenerationData(_Bounds, BlocksController.Blocks), true);

            if (!SystemController.Current.TryQueueJob(chunkMeshingJob, out _JobIdentity))
            {
                return;
            }

            SystemController.Current.JobFinished += OnJobFinished;

            Meshing = true;
        }

        private void ApplyMesh(ChunkMeshingJob chunkMeshingJob)
        {
            chunkMeshingJob.SetMesh(ref _Mesh);
            OnMeshChanged(this, new ChunkChangedEventArgs(_Bounds, Directions.CardinalDirectionAxes));
        }


        #region EVENTS

        public event ChunkChangedEventHandler MeshChanged;

        private void OnMeshChanged(object sender, ChunkChangedEventArgs args)
        {
            MeshChanged?.Invoke(sender, args);
        }

        private void OnJobFinished(object sender, JobEventArgs args)
        {
            if ((args.Job.Identity != _JobIdentity) || !(args.Job is ChunkMeshingJob chunkMeshingJob))
            {
                return;
            }

            MainThreadActionsController.Current.PushAction(new MainThreadAction(default,
                () => ApplyMesh(chunkMeshingJob)));
            SystemController.Current.JobFinished -= OnJobFinished;
            _JobIdentity = null;
        }

        #endregion
    }
}
