#region

using System;
using UnityEngine;
using Wyd.Game.World.Chunks;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    public class ChunkMeshController : ActivationStateChunkController
    {
        #region INSTANCE MEMBERS

        private Mesh _Mesh;
        private object _JobIdentity;
        private bool _UpdateRequested;
        private ChunkMeshingJob _PendingMeshData;

        public bool Meshing { get; private set; }

        #endregion

        #region SERIALIZED MEMBERS

        [SerializeField]
        private MeshFilter MeshFilter;

        [SerializeField]
        private ChunkBlocksController BlocksController;

        [SerializeField]
        private ChunkTerrainController TerrainController;

        #endregion

        public override void Awake()
        {
            base.Awake();

            _Mesh = new Mesh();
            MeshFilter.sharedMesh = _Mesh;
        }

        public void Update()
        {
            
        }

        #region DE/ACTIVATION

        public override void Activate(Vector3 position, bool setPosition)
        {
            base.Activate(position, setPosition);
            ClearInternalData();
        }

        public override void Deactivate()
        {
            base.Deactivate();
            ClearInternalData();
        }

        private void ClearInternalData()
        {
            if (_Mesh != default)
            {
                _Mesh.Clear();
            }

            _JobIdentity = _PendingMeshData = null;
            Meshing = false;
        }

        #endregion

        public void RequestUpdate()
        {
            if (!_UpdateRequested)
            {
                _UpdateRequested = true;
            }
        }
    }
}
