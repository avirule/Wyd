#region

using System.ComponentModel;
using Serilog;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Wyd.Controllers.State;
using Wyd.Game;
using Wyd.Game.Entities;
using Wyd.Game.World;
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

        private IEntity _CurrentLoader;
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
                OnChanged(sender, args);
            };
            TerrainController.TerrainChanged += (sender, args) =>
            {
                MeshController.FlagForUpdate();
                OnChanged(sender, args);
            };

            MeshRenderer.materials = TextureController.Current.TerrainMaterials;
            _Visible = MeshRenderer.enabled;
        }

        private void Start()
        {
            if (_CurrentLoader == default)
            {
                Log.Warning(
                    $"Chunk at position {OriginPoint} has been initialized without a loader. This is possibly an error.");
            }
            else
            {
                OnCurrentLoaderChangedChunk(this, _CurrentLoader.ChunkPosition);
            }

            OptionsController.Current.PropertyChanged += OnShadowDistanceChanged;
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

        protected override void OnDisable()
        {
            base.OnDisable();

            if (_CurrentLoader != default)
            {
                _CurrentLoader.ChunkPositionChanged -= OnCurrentLoaderChangedChunk;
                _CurrentLoader = default;
            }
        }

        private void OnDestroy()
        {
            OnDestroyed(this, new ChunkChangedEventArgs(OriginPoint, Directions.CardinalDirectionAxes));

            if (OptionsController.Current != null)
            {
                OptionsController.Current.PropertyChanged -= OnShadowDistanceChanged;
            }
        }

        public void FlagMeshForUpdate()
        {
            MeshController.FlagForUpdate();
        }


        #region DE/ACTIVATION

        public void Activate(int3 position)
        {
            _SelfTransform.position = (float3)position;

            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            gameObject.SetActive(false);
        }

        public void AssignLoader(ref IEntity loader)
        {
            if (loader == default)
            {
                return;
            }

            _CurrentLoader = loader;
            _CurrentLoader.ChunkPositionChanged += OnCurrentLoaderChangedChunk;
        }

        #endregion


        #region INTERNAL STATE CHECKS

        private static bool IsWithinLoaderRange(float3 difference) =>
            math.all(difference
                     <= (Size
                         * (OptionsController.Current.RenderDistance
                            + OptionsController.Current.PreLoadChunkDistance)));

        private static bool IsWithinRenderDistance(float3 difference) =>
            math.all(difference <= (Size * OptionsController.Current.RenderDistance));

        private static bool IsWithinShadowsDistance(float3 difference) =>
            math.all(difference <= (Size * OptionsController.Current.ShadowDistance));

        #endregion


        #region EVENT

        public event ChunkChangedEventHandler Changed;
        public event ChunkChangedEventHandler DeactivationCallback;
        public event ChunkChangedEventHandler Destroyed;

        private void OnChanged(object sender, ChunkChangedEventArgs args)
        {
            Changed?.Invoke(sender, args);
        }

        protected virtual void OnDestroyed(object sender, ChunkChangedEventArgs args)
        {
            Destroyed?.Invoke(sender, args);
        }

        private void OnCurrentLoaderChangedChunk(object sender, int3 newChunkPosition)
        {
            if (math.all(OriginPoint == newChunkPosition))
            {
                return;
            }

            float3 difference = math.abs(OriginPoint - newChunkPosition);
            difference.y = 0; // always load all chunks on y axis

            if (!IsWithinLoaderRange(difference))
            {
                DeactivationCallback?.Invoke(this,
                    new ChunkChangedEventArgs(OriginPoint, Directions.CardinalDirectionAxes));
                return;
            }

            Visible = IsWithinRenderDistance(difference);
            RenderShadows = IsWithinShadowsDistance(difference);
        }

        private void OnShadowDistanceChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName.Equals(nameof(OptionsController.Current.ShadowDistance)))
            {
                RenderShadows = IsWithinShadowsDistance(math.abs(OriginPoint - _CurrentLoader.ChunkPosition));
            }
        }

        #endregion
    }
}
