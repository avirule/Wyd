#region

using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Wyd.Controllers.State;
using Wyd.Game.World.Chunks;
using Wyd.Game.World.Chunks.Events;
using Wyd.System;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    public class ChunkController : ActivationStateChunkController
    {
        public static readonly int3 Size = new int3(32);

        #region INSTANCE MEMBERS

        private bool _Visible;
        private bool _RenderShadows;

        public TerrainStep CurrentStep => TerrainController.CurrentStep;

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
                MeshRenderer.enabled = _Visible;
            }
        }

        #endregion


        #region SERIALIZED MEMBERS

        [SerializeField]
        private MeshRenderer MeshRenderer;

        [SerializeField]
        public ChunkBlocksController BlocksController;

        [SerializeField]
        private ChunkTerrainController TerrainController;

        [SerializeField]
        private ChunkMeshController MeshController;

#if UNITY_EDITOR

        [SerializeField]
        [ReadOnlyInspectorField]
        public Vector3 MinimumPoint;

        [SerializeField]
        [ReadOnlyInspectorField]
        public Vector3 MaximumPoint;

        [SerializeField]
        [ReadOnlyInspectorField]
        public Vector3 Extents;


#endif

        #endregion

        protected override void Awake()
        {
            base.Awake();

            BlocksController.BlocksChanged += (sender, args) =>
            {
                MeshController.FlagForUpdate();
                OnLocalTerrainChanged(sender, args);
            };
            TerrainController.TerrainChanged += (sender, args) =>
            {
                MeshController.FlagForUpdate();
                OnLocalTerrainChanged(sender, args);
            };
            MeshController.MeshChanged += OnMeshChanged;

            MeshRenderer.materials = TextureController.Current.TerrainMaterials;
            _Visible = MeshRenderer.enabled;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

#if UNITY_EDITOR

            MinimumPoint = OriginPoint;
            MaximumPoint = OriginPoint + Size;
            Extents = WydMath.ToFloat(Size) / 2f;

#endif

            _Visible = MeshRenderer.enabled;
        }

        public void FlagMeshForUpdate()
        {
            MeshController.FlagForUpdate();
        }


        #region DE/ACTIVATION

        public void Activate(float3 position)
        {
            _SelfTransform.position = position;

            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            gameObject.SetActive(false);
        }

        #endregion


        #region EVENT

        public event ChunkChangedEventHandler TerrainChanged;
        public event ChunkChangedEventHandler MeshChanged;

        private void OnLocalTerrainChanged(object sender, ChunkChangedEventArgs args)
        {
            TerrainChanged?.Invoke(sender, args);
        }

        private void OnMeshChanged(object sender, ChunkChangedEventArgs args)
        {
            MeshChanged?.Invoke(sender, args);
        }

        #endregion
    }
}
