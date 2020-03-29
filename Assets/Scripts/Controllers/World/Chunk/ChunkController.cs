#region

using Serilog;
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
        public static readonly Vector3Int Size = new Vector3Int(32, 32, 32);
        public static readonly int SizeProduct = Size.Product();
        public static readonly int YIndexStep = Size.x * Size.z;

        #region INSTANCE MEMBERS

        private IEntity _CurrentLoader;
        private bool _Visible;
        private bool _RenderShadows;

        public Vector3 Position => _Bounds.min;
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

        public ChunkController(Bounds bounds) : base(bounds) { }

        #endregion

        protected override void Awake()
        {
            base.Awake();

            BlocksController.BlocksChanged += (sender, args) => MeshController.FlagForUpdate();
            TerrainController.TerrainChanged += (sender, args) => MeshController.FlagForUpdate();
            MeshController.MeshChanged += OnChanged;

            foreach (Material material in MeshRenderer.materials)
            {
                material.SetTexture(TextureController.MainTexPropertyID, TextureController.Current.TerrainTexture);
            }

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
                OnCurrentLoaderChangedChunk(this, _CurrentLoader.CurrentChunk);
            }

            OptionsController.Current.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName.Equals(nameof(OptionsController.Current.ShadowDistance)))
                {
                    RenderShadows = IsWithinShadowsDistance((Position - _CurrentLoader.CurrentChunk).Abs());
                }
            };
        }

        private void OnDestroy()
        {
            OnDestroyed(this, new ChunkChangedEventArgs(_Bounds, Directions.CardinalDirectionsVector3));
        }

        public void FlagMeshForUpdate()
        {
            MeshController.FlagForUpdate();
        }

        #region DE/ACTIVATION

        public void Activate(Vector3 position)
        {
            base.Activate(position, true);
            BlocksController.Activate(position, false);
            TerrainController.Activate(position, false);
            MeshController.Activate(position, false);
            gameObject.SetActive(true);

            _Visible = MeshRenderer.enabled;
        }

        public override void Deactivate()
        {
            base.Deactivate();
            BlocksController.Deactivate();
            TerrainController.Deactivate();
            MeshController.Deactivate();

            if (_CurrentLoader != default)
            {
                _CurrentLoader.ChunkPositionChanged -= OnCurrentLoaderChangedChunk;
                _CurrentLoader = default;
            }

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

        private static bool IsWithinLoaderRange(Vector3 difference) =>
            difference.AllLessThanOrEqual(Size
                                          * (OptionsController.Current.RenderDistance
                                             + OptionsController.Current.PreLoadChunkDistance));

        private static bool IsWithinRenderDistance(Vector3 difference) =>
            difference.AllLessThanOrEqual(Size * OptionsController.Current.RenderDistance);

        private static bool IsWithinShadowsDistance(Vector3 difference) =>
            difference.AllLessThanOrEqual(Size * OptionsController.Current.ShadowDistance);

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

        private void OnCurrentLoaderChangedChunk(object sender, Vector3 newChunkPosition)
        {
            if (Position == newChunkPosition)
            {
                return;
            }

            Vector3 difference = (Position - newChunkPosition).Abs();

            if (!IsWithinLoaderRange(difference))
            {
                DeactivationCallback?.Invoke(this,
                    new ChunkChangedEventArgs(_Bounds, Directions.CardinalDirectionsVector3));
                return;
            }

            Visible = IsWithinRenderDistance(difference);
            RenderShadows = IsWithinShadowsDistance(difference);
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
