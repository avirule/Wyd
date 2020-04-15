#region

using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using Wyd.Game.World.Chunks;
using Wyd.System;
using Wyd.System.Collections;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    public class ChunkMeshController : ActivationStateChunkController
    {
        #region INSTANCE MEMBERS

        private Mesh _Mesh;

        #endregion

        #region SERIALIZED MEMBERS

        [SerializeField]
        private MeshFilter MeshFilter;

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

        public void BeginGeneratingMesh(OctreeNode blocks, CancellationToken token, AsyncJobEventHandler callback)
        {
            ChunkMeshingJob asyncJob = new ChunkMeshingJob(token, OriginPoint, blocks, true);

            if (callback != null)
            {
                asyncJob.WorkFinished += callback;
            }

            Task.Run(async () => await AsyncJobScheduler.QueueAsyncJob(asyncJob), token);
        }

        public void ApplyMesh(ChunkMeshData chunkMeshData)
        {
            if ((chunkMeshData.Vertices.Count == 0)
                || ((chunkMeshData.Triangles.Count == 0)
                    && (chunkMeshData.TransparentTriangles.Count == 0)))
            {
                return;
            }

            _Mesh.Clear();

            _Mesh.subMeshCount = 2;
            _Mesh.indexFormat = chunkMeshData.Vertices.Count > 65000
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;

            _Mesh.MarkDynamic();
            _Mesh.SetVertices(chunkMeshData.Vertices);
            _Mesh.SetTriangles(chunkMeshData.Triangles, 0);
            _Mesh.SetTriangles(chunkMeshData.TransparentTriangles, 1);

            // check uvs count in case of no UVs to apply to mesh
            if (chunkMeshData.UVs.Count > 0)
            {
                _Mesh.SetUVs(0, chunkMeshData.UVs);
            }

#if UNITY_EDITOR

            // update debug data when mesh changed
            VertexCount = _Mesh.vertices.Length;
            TrianglesCount = _Mesh.triangles.Length;
            UVsCount = _Mesh.uv.Length;
            TotalTimesMeshed += 1;
            TimesMeshed += 1;

#endif
        }
    }
}
