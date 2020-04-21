#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Serilog;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Game;
using Wyd.Game.World.Blocks;
using Wyd.Game.World.Chunks;
using Wyd.Game.World.Chunks.Events;
using Wyd.System;
using Wyd.System.Collections;
using Wyd.System.Extensions;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    [Flags]
    public enum ChunkState
    {
        Terrain = 0b0000_0001,
        AwaitingTerrain = 0b0000_0010,
        TerrainComplete = 0b0000_1000,
        TerrainMask = 0b0000_1111,
        UpdateMesh = 0b0001_0000,
        Meshing = 0b0010_0000,
        MeshDataPending = 0b0100_0000,
        Meshed = 0b1000_0000
    }

    public class ChunkController : ActivationStateChunkController, IPerFrameIncrementalUpdate
    {
        public const int SIZE = 32;

        private static readonly ObjectCache<BlockAction> _blockActionsCache = new ObjectCache<BlockAction>(true, 1024);

        public static readonly int3 SizeCubed = new int3(SIZE);
        public static readonly int3 SizeExtents = new int3(SIZE / 2);


        #region Instance Members

        private CancellationTokenSource _CancellationTokenSource;
        private Queue<BlockAction> _BlockActions;
        private OctreeNode<ushort> _Blocks;
        private Mesh _Mesh;

        private long _Active;
        private long _State;
        private ChunkMeshData _PendingMeshData;

        public bool Active
        {
            get => Convert.ToBoolean(Interlocked.Read(ref _Active));
            set => Interlocked.Exchange(ref _Active, Convert.ToInt64(value));
        }

        public ref OctreeNode<ushort> Blocks => ref _Blocks;

        public ChunkState ChunkState
        {
            get => (ChunkState)Interlocked.Read(ref _State);
            private set
            {
                Interlocked.Exchange(ref _State, (long)value);

#if UNITY_EDITOR

                BinaryState = Convert.ToString(unchecked((byte)ChunkState), 2);

#endif
            }
        }

        #endregion


        #region Serialized Members

        [SerializeField]
        private ChunkTerrainController TerrainController;

        [SerializeField]
        private ChunkMeshController MeshController;

#if UNITY_EDITOR

        [SerializeField]
        [ReadOnlyInspectorField]
        private Vector3 MinimumPoint;

        [SerializeField]
        [ReadOnlyInspectorField]
        private Vector3 MaximumPoint;

        [SerializeField]
        [ReadOnlyInspectorField]
        private Vector3 Extents;

        [SerializeField]
        [ReadOnlyInspectorField]
        private string BinaryState;
#endif

        #endregion


        protected override void Awake()
        {
            base.Awake();

            _BlockActions = new Queue<BlockAction>();
            _Mesh = new Mesh();

            void FlagUpdateMesh(object sender, ChunkChangedEventArgs args)
            {
                FlagMeshForUpdate();
            }

            BlocksChanged += FlagUpdateMesh;
            TerrainChanged += FlagUpdateMesh;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            PerFrameUpdateController.Current.RegisterPerFrameUpdater(20, this);

            _CancellationTokenSource = new CancellationTokenSource();

            ChunkState = ChunkState.Terrain;

            Active = gameObject.activeSelf;

#if UNITY_EDITOR

            MinimumPoint = OriginPoint;
            MaximumPoint = OriginPoint + SizeCubed;
            Extents = WydMath.ToFloat(SizeCubed) / 2f;

#endif
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            PerFrameUpdateController.Current.DeregisterPerFrameUpdater(20, this);

            if (_Mesh != null)
            {
                _Mesh.Clear();
            }

            _CancellationTokenSource.Cancel();
            _BlockActions.Clear();
            _Blocks = null;

            ChunkState = ChunkState.Terrain;

            Active = gameObject.activeSelf;
        }

        public void FrameUpdate()
        {
            if (!WorldController.Current.ReadyForGeneration
                || ChunkState.HasState(ChunkState.AwaitingTerrain)
                || (ChunkState.HasState(ChunkState.TerrainComplete)
                    && ChunkState.HasState(ChunkState.Meshed)
                    && !ChunkState.HasState(ChunkState.UpdateMesh)))
            {
                return;
            }


            if (ChunkState.HasState(ChunkState.Terrain))
            {
                ChunkState |= (ChunkState & ~ChunkState.TerrainMask) | ChunkState.AwaitingTerrain;
                TerrainController.BeginTerrainGeneration(_CancellationTokenSource.Token, OnTerrainGenerationFinished);
            }

            if (!ChunkState.HasState(ChunkState.TerrainComplete))
            {
                return;
            }

            if (ChunkState.HasState(ChunkState.MeshDataPending) && (_PendingMeshData != null))
            {
                MeshController.ApplyMesh(_PendingMeshData);
                _PendingMeshData = null;
                ChunkState = (ChunkState & ~ChunkState.MeshDataPending) | ChunkState.Meshed;
                OnMeshChanged(this, new ChunkChangedEventArgs(OriginPoint, Enumerable.Empty<float3>()));
            }

            if (!ChunkState.HasState(ChunkState.Meshing)
                && !ChunkState.HasState(ChunkState.MeshDataPending)
                && (!ChunkState.HasState(ChunkState.Meshed) || ChunkState.HasState(ChunkState.UpdateMesh))
                && WorldController.Current.GetNeighbors(OriginPoint).All(chunkController =>
                    chunkController.ChunkState.HasState(ChunkState.TerrainComplete)))
            {
                if (Blocks.IsUniform
                    && ((Blocks.Value == BlockController.AirID)
                        || WorldController.Current.GetNeighbors(OriginPoint)
                            .All(chunkController => chunkController.Blocks.IsUniform)))
                {
                    ChunkState = (ChunkState & ~ChunkState.MeshDataPending) | ChunkState.Meshed;
                }
                else
                {
                    ChunkState = (ChunkState & ~(ChunkState.Meshed | ChunkState.UpdateMesh)) | ChunkState.Meshing;
                    MeshController.BeginGeneratingMesh(Blocks, _CancellationTokenSource.Token, OnMeshingFinished);
                }
            }
        }

        public IEnumerable IncrementalFrameUpdate()
        {
            if (!ChunkState.HasState(ChunkState.TerrainComplete))
            {
                yield break;
            }

            while (_BlockActions.Count > 0)
            {
                BlockAction blockAction = _BlockActions.Dequeue();

                ProcessBlockAction(blockAction);

                _blockActionsCache.CacheItem(ref blockAction);

                yield return null;
            }
        }

        public void FlagMeshForUpdate()
        {
            if (!ChunkState.HasState(ChunkState.UpdateMesh))
            {
                ChunkState |= ChunkState.UpdateMesh;
            }
        }


        #region De/Activation

        public void Activate(float3 position)
        {
            _SelfTransform.SetPositionAndRotation(position, quaternion.identity);

            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            gameObject.SetActive(false);
        }

        #endregion


        #region Block Actions

        private void ProcessBlockAction(BlockAction blockAction)
        {
            ModifyBlockPosition(blockAction.GlobalPosition, blockAction.Id);

            OnBlocksChanged(this,
                TryGetNeighborsRequiringUpdateNormals(blockAction.GlobalPosition, out IEnumerable<float3> normals)
                    ? new ChunkChangedEventArgs(OriginPoint, normals)
                    : new ChunkChangedEventArgs(OriginPoint, Enumerable.Empty<float3>()));
        }

        private bool TryGetNeighborsRequiringUpdateNormals(float3 globalPosition, out IEnumerable<float3> normals)
        {
            normals = Enumerable.Empty<float3>();

            float3 localPosition = globalPosition - (OriginPoint + (WydMath.ToFloat(SizeCubed) / 2f));
            float3 localPositionSign = math.sign(localPosition);
            float3 localPositionAbs = math.abs(math.ceil(localPosition + (new float3(0.5f) * localPositionSign)));

            if (!math.any(localPositionAbs == 16f))
            {
                return false;
            }

            normals = WydMath.ToComponents(math.floor(localPositionAbs / 16f) * localPositionSign);
            return true;
        }

        private void ModifyBlockPosition(float3 globalPosition, ushort newId)
        {
            if (!Blocks.ContainsMinBiased(globalPosition))
            {
                return;
            }

            Blocks.SetPoint(globalPosition, newId);
        }

        public bool TryGetBlock(float3 globalPosition, out ushort blockId)
        {
            blockId = 0;

            if (Blocks == null)
            {
#if UNITY_EDITOR

                Log.Verbose(
                    $"'{nameof(Blocks)}' is null (origin: {OriginPoint}, state: {Convert.ToString((int)ChunkState, 2)}).");

#endif

                return false;
            }
            else if (!Blocks.TryGetPoint(globalPosition, out blockId))
            {
#if UNITY_EDITOR

                Log.Verbose($"Failed to get block data from point {globalPosition} in chunk at origin {OriginPoint}.");

#endif

                return false;
            }
            else
            {
                return true;
            }
        }

        public bool TryPlaceBlock(float3 globalPosition, ushort newBlockId) =>
            (Blocks != null)
            && Blocks.ContainsMinBiased(globalPosition)
            && TryGetBlock(globalPosition, out ushort blockId)
            && (blockId != newBlockId)
            && AllocateBlockAction(globalPosition, newBlockId);

        private bool AllocateBlockAction(float3 globalPosition, ushort id)
        {
            BlockAction blockAction = _blockActionsCache.Retrieve();
            blockAction.SetData(globalPosition, id);
            _BlockActions.Enqueue(blockAction);
            return true;
        }

        #endregion


        #region Events

        public event ChunkChangedEventHandler BlocksChanged;
        public event ChunkChangedEventHandler TerrainChanged;
        public event ChunkChangedEventHandler MeshChanged;


        private void OnBlocksChanged(object sender, ChunkChangedEventArgs args)
        {
            BlocksChanged?.Invoke(sender, args);
        }

        private void OnLocalTerrainChanged(object sender, ChunkChangedEventArgs args)
        {
            TerrainChanged?.Invoke(sender, args);
        }

        private void OnTerrainGenerationFinished(object sender, AsyncJobEventArgs args)
        {
            args.AsyncJob.WorkFinished -= OnTerrainGenerationFinished;

            if (!Active)
            {
                return;
            }

            ((ChunkBuildingJob)args.AsyncJob).GetGeneratedBlockData(out _Blocks);
            ChunkState = (ChunkState & ~ChunkState.TerrainMask) | ChunkState.TerrainComplete;
            OnLocalTerrainChanged(sender,
                new ChunkChangedEventArgs(OriginPoint, Directions.AllDirectionAxes.Select(WydMath.ToFloat)));
        }

        private void OnMeshChanged(object sender, ChunkChangedEventArgs args)
        {
            MeshChanged?.Invoke(sender, args);
        }

        private void OnMeshingFinished(object sender, AsyncJobEventArgs args)
        {
            args.AsyncJob.WorkFinished -= OnMeshingFinished;

            if (!(args.AsyncJob is ChunkMeshingJob chunkMeshingJob) || !Active)
            {
                return;
            }

            _PendingMeshData = chunkMeshingJob.GetMeshData();
            ChunkState = (ChunkState & ~ChunkState.Meshing) | ChunkState.MeshDataPending;
        }

        #endregion
    }
}
