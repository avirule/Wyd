#region

using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Game.World.Chunks;
using Wyd.System;
using Wyd.System.Collections;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    public class ChunkMeshController : ActivationStateChunkController
    {
        #region Instance Members

        private Mesh _Mesh;

        #endregion


        #region Serialized Members

        [SerializeField]
        private MeshFilter MeshFilter;

        [SerializeField]
        private MeshRenderer MeshRenderer;

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
            MeshRenderer.materials = TextureController.Current.AllBlocksMaterials;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            ClearInternalData();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            ClearInternalData();
        }

        private void ClearInternalData()
        {
            if (_Mesh != default)
            {
                _Mesh.Clear();
            }

#if UNITY_EDITOR

            TimesMeshed = VertexCount = TrianglesCount = UVsCount = 0;

#endif
        }

        public void BeginGeneratingMesh(CancellationToken token, AsyncJobEventHandler callback, INodeCollection<ushort> blocks,
            out object jobIdentity)
        {
            // todo make setting for improved meshing
            ChunkMeshingJob asyncJob = new ChunkMeshingJob(token, OriginPoint, blocks, OptionsController.Current.GPUAcceleration);

            if (callback != null)
            {
                asyncJob.WorkFinished += callback;
            }

            jobIdentity = asyncJob.Identity;

            AsyncJobScheduler.QueueAsyncJob(asyncJob);
        }

        public void ApplyMesh(ChunkMeshingJob meshingJob)
        {
            meshingJob.ApplyMeshData(ref _Mesh);
            meshingJob.CacheMesher();

            MeshRenderer.enabled = _Mesh.vertexCount > 0;

#if UNITY_EDITOR

            VertexCount = _Mesh.vertices.Length;
            TrianglesCount = _Mesh.triangles.Length;
            UVsCount = _Mesh.uv.Length;
            TotalTimesMeshed += 1;
            TimesMeshed += 1;

#endif
        }
    }
}
