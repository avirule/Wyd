#region

using System.ComponentModel;
using Serilog;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Wyd.Controllers.State;
using Wyd.Game;
using Wyd.Game.Entities;
using Wyd.Game.World.Chunks.Events;
using Wyd.System;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    public class ChunkController : ActivationStateChunkController
    {
        // todo Size should be somewhere that makes more sense, I think
        public static readonly int3 Size = new int3(32);

        #region INSTANCE MEMBERS

        private IEntity _CurrentLoader;
        private bool _Visible;
        private bool _RenderShadows;

        public int3 Position => WydMath.ToInt(_Volume.MinPoint);
        public GenerationData.GenerationStep CurrentStep => TerrainController.CurrentStep;

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

            // todo implement chunk ticks
//            double waitTime = TimeSpan
//                .FromTicks((DateTime.Now.Ticks - WorldController.Current.InitialTick) %
//                           WorldController.Current.WorldTickRate.Ticks)
//                .TotalSeconds;
//            InvokeRepeating(nameof(Tick), (float) waitTime, (float) WorldController.Current.WorldTickRate.TotalSeconds);
        }

        private void Start()
        {
            if (_CurrentLoader == default)
            {
                Log.Warning(
                    $"Chunk at position {Position} has been initialized without a loader. This is possibly an error.");
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

            MinimumPoint = _Volume.MinPoint;
            MaximumPoint = _Volume.MaxPoint;
            Extents = _Volume.Extents;

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
            OnDestroyed(this, new ChunkChangedEventArgs(_Volume, Directions.CardinalDirectionAxes));

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


        #region EVENTS

        // todo chunk load failed event

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
            if (math.all(Position == newChunkPosition))
            {
                return;
            }

            float3 difference = math.abs(Position - newChunkPosition);
            difference.y = 0; // always load all chunks on y axis

            if (!IsWithinLoaderRange(difference))
            {
                DeactivationCallback?.Invoke(this,
                    new ChunkChangedEventArgs(_Volume, Directions.CardinalDirectionAxes));
                return;
            }

            Visible = IsWithinRenderDistance(difference);
            RenderShadows = IsWithinShadowsDistance(difference);
        }

        private void OnShadowDistanceChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName.Equals(nameof(OptionsController.Current.ShadowDistance)))
            {
                RenderShadows = IsWithinShadowsDistance(math.abs(Position - _CurrentLoader.ChunkPosition));
            }
        }

        #endregion


        #region SERIALIZATION

        // public byte[] ToSerialized()
        // {
        //     const byte run_length_size = sizeof(uint);
        //     const byte value_size = sizeof(ushort);
        //     const byte node_size = value_size + run_length_size;
        //
        //     // 8 bytes for runlength and value
        //     byte[] bytes = new byte[_Blocks.Count * node_size];
        //
        //     uint index = 0;
        //     foreach (RLENode<ushort> node in _Blocks)
        //     {
        //         // copy runlength (int, 4 bytes) to position of i
        //         Array.Copy(BitConverter.GetBytes(node.RunLength), 0, bytes, index, run_length_size);
        //         // copy node value, also 4 bytes, to position of i + 4 bytes from runlength
        //         Array.Copy(BitConverter.GetBytes(node.Value), 0, bytes, index + value_size, value_size);
        //
        //         index += node_size;
        //     }
        //
        //     return bytes;
        // }
        //
        // public bool FromSerialized(byte[] data)
        // {
        //     const byte run_length_size = sizeof(uint);
        //     const byte value_size = sizeof(ushort);
        //     const byte node_size = value_size + run_length_size;
        //
        //     if ((data.Length % node_size) != 0)
        //     {
        //         // data is misformatted, so do nothing
        //         return false;
        //     }
        //
        //     for (int i = 0; i < data.Length; i += node_size)
        //     {
        //         // todo fix this to work with linked list
        //         uint runLength = BitConverter.ToUInt32(data, i);
        //         ushort value = BitConverter.ToUInt16(data, i + run_length_size);
        //
        //         _Blocks.AddLast(new RLENode<ushort>(runLength, value));
        //     }
        //
        //     _ChunkGenerator.SetSkipBuilding(true);
        //
        //     return true;
        // }

        #endregion
    }
}
