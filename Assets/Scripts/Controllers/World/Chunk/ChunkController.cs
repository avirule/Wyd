#region

using System;
using Serilog;
using UnityEngine;
using UnityEngine.Rendering;
using Wyd.Controllers.State;
using Wyd.Game;
using Wyd.Game.Entities;
using Wyd.Game.World.Chunks;
using Wyd.System;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    public class ChunkController : MonoBehaviour
    {
        public static readonly Vector3Int Size = new Vector3Int(32, 256, 32);
        public static readonly int SizeProduct = Size.Product();
        public static readonly int YIndexStep = Size.x * Size.z;

        #region INSTANCE MEMBERS

        private Bounds _Bounds;
        private Transform _SelfTransform;
        private IEntity _CurrentLoader;

        private bool _Visible;
        private bool _RenderShadows;


        public Vector3 Position => _Bounds.min;

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
        public ChunkGenerationController GenerationController;

        #endregion


        #region UNITY BUILT-INS

        private void Awake()
        {
            _SelfTransform = transform;
            Vector3 position = _SelfTransform.position;
            _Bounds.SetMinMax(position, position + Size);

            BlocksController.BlocksChanged += (sender, args) => { GenerationController.RequestMeshUpdate(); };

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

        private void Update() { }

        private void OnDestroy()
        {
            OnDestroyed(this, new ChunkChangedEventArgs(_Bounds, Directions.CardinalDirectionsVector3));
        }

        #endregion

        #region DE/ACTIVATION

        public void Activate(Vector3 position)
        {
            _SelfTransform.position = position;
            _Bounds.SetMinMax(position, position + Size);
            _Visible = MeshRenderer.enabled;

            GenerationController.Activate();
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            if (_CurrentLoader != default)
            {
                _CurrentLoader.ChunkPositionChanged -= OnCurrentLoaderChangedChunk;
                _CurrentLoader = default;
            }

            StopAllCoroutines();
            GenerationController.Deactivate();
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


        #region HELPER METHODS



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

        public event EventHandler<ChunkChangedEventArgs> MeshChanged;
        public event EventHandler<ChunkChangedEventArgs> DeactivationCallback;
        public event EventHandler<ChunkChangedEventArgs> Destroyed;

        protected virtual void OnMeshChanged(object sender, ChunkChangedEventArgs args)
        {
            MeshChanged?.Invoke(sender, args);
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
    }
}
