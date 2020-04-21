#region

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Serilog;
using Unity.Mathematics;
using UnityEditor.U2D;
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
        Terrain = 0b0_0000_0001,
        Detail = 0b0_0000_0010,
        AwaitingTerrain = 0b0_0000_0100,
        TerrainComplete = 0b0_0000_1000,
        TerrainMask = 0b0_0000_1111,
        UpdateMesh = 0b0_0001_0000,
        Meshing = 0b0_0010_0000,
        MeshDataPending = 0b0_0100_0000,
        Meshed = 0b0_1000_0000,
        BlockActionsProcessing = 0b1_0000_0000
    }

    public class ChunkController : ActivationStateChunkController, IPerFrameIncrementalUpdate
    {
        public const int SIZE = 32;
        public const int SIZE_CUBED = SIZE * SIZE * SIZE;

        private static readonly ObjectCache<BlockAction> _blockActionsCache = new ObjectCache<BlockAction>(true, 1024);

        public static readonly int3 Size3D = new int3(SIZE);
        public static readonly int3 Size3DExtents = new int3(SIZE / 2);


        #region Instance Members

        private CancellationTokenSource _CancellationTokenSource;
        private ConcurrentQueue<BlockAction> _BlockActions;
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

        public OctreeNode<ushort> Blocks => _Blocks;

        public ChunkState State
        {
            get => (ChunkState)Interlocked.Read(ref _State);
            private set
            {
                Interlocked.Exchange(ref _State, (long)value);

#if UNITY_EDITOR

                BinaryState = Convert.ToString(unchecked((byte)State), 2).PadLeft(8, '0');

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

        [SerializeField]
        private bool Regenerate;
#endif

        #endregion


        protected override void Awake()
        {
            base.Awake();

            _BlockActions = new ConcurrentQueue<BlockAction>();
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

            State = ChunkState.Terrain;

            Active = gameObject.activeSelf;

#if UNITY_EDITOR

            MinimumPoint = OriginPoint;
            MaximumPoint = OriginPoint + Size3D;
            Extents = WydMath.ToFloat(Size3D) / 2f;

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
            _BlockActions = new ConcurrentQueue<BlockAction>();
            _Blocks = null;

            State = ChunkState.Terrain;

            Active = gameObject.activeSelf;
        }

        public void FrameUpdate()
        {
            #if UNITY_EDITOR

            if (Regenerate)
            {
                State = ChunkState.Terrain;
                _PendingMeshData = null;
                Regenerate = false;
            }

            #endif

            if (!WorldController.Current.ReadyForGeneration
                || State.HasState(ChunkState.AwaitingTerrain)
                || (State.HasState(ChunkState.TerrainComplete)
                    && State.HasState(ChunkState.Meshed)
                    && !State.HasState(ChunkState.UpdateMesh)))
            {
                return;
            }


            if (State.HasState(ChunkState.Terrain))
            {
                State |= (State & ~ChunkState.TerrainMask) | ChunkState.AwaitingTerrain;
                TerrainController.BeginTerrainGeneration(_CancellationTokenSource.Token, OnTerrainGenerationFinished);
            }
            else if (State.HasState(ChunkState.Detail)
                     && WorldController.Current.GetNeighboringChunks(OriginPoint).All(chunkController =>
                         chunkController.State.HasState(ChunkState.Detail | ChunkState.TerrainComplete)))
            {
                State |= (State & ~ChunkState.TerrainMask) | ChunkState.AwaitingTerrain;
                TerrainController.BeginTerrainDetailing(_CancellationTokenSource.Token, OnTerrainDetailingFinished,
                    ref _Blocks);
            }

            if (!State.HasState(ChunkState.TerrainComplete))
            {
                return;
            }

            if (State.HasState(ChunkState.MeshDataPending) && (_PendingMeshData != null))
            {
                MeshController.ApplyMesh(_PendingMeshData);
                _PendingMeshData = null;
                State = (State & ~ChunkState.MeshDataPending) | ChunkState.Meshed;
                OnMeshChanged(this, new ChunkChangedEventArgs(OriginPoint, Enumerable.Empty<float3>()));
            }

            if (!State.HasState(ChunkState.BlockActionsProcessing)
                && !State.HasState(ChunkState.Meshing)
                && !State.HasState(ChunkState.MeshDataPending)
                && (!State.HasState(ChunkState.Meshed) || State.HasState(ChunkState.UpdateMesh))
                && WorldController.Current.GetNeighboringChunks(OriginPoint).All(chunkController =>
                    chunkController.State.HasState(ChunkState.TerrainComplete)))
            {
                if (Blocks.IsUniform
                    && ((Blocks.Value == BlockController.AirID)
                        || WorldController.Current.GetNeighboringChunks(OriginPoint)
                            .All(chunkController => chunkController.Blocks.IsUniform)))
                {
                    State = (State & ~(ChunkState.MeshDataPending | ChunkState.UpdateMesh)) | ChunkState.Meshed;
                }
                else
                {
                    State = (State & ~(ChunkState.Meshed | ChunkState.UpdateMesh)) | ChunkState.Meshing;
                    MeshController.BeginGeneratingMesh(Blocks, _CancellationTokenSource.Token, OnMeshingFinished);
                }
            }
        }

        public IEnumerable IncrementalFrameUpdate()
        {
            if (!State.HasState(ChunkState.TerrainComplete))
            {
                yield break;
            }

            State |= ChunkState.BlockActionsProcessing;

            while (_BlockActions.TryDequeue(out BlockAction blockAction))
            {
                ProcessBlockAction(blockAction);

                _blockActionsCache.CacheItem(ref blockAction);

                yield return null;
            }

            State &= ~ChunkState.BlockActionsProcessing;
        }

        public void FlagMeshForUpdate()
        {
            if (!State.HasState(ChunkState.UpdateMesh))
            {
                State |= ChunkState.UpdateMesh;
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

        private void ModifyBlockPosition(float3 globalPosition, ushort newId)
        {
            Blocks.UncheckedSetPoint(globalPosition, newId);
        }

        private bool TryGetNeighborsRequiringUpdateNormals(float3 globalPosition, out IEnumerable<float3> normals)
        {
            normals = Enumerable.Empty<float3>();

            // set local position to center point of chunk
            float3 localPosition = globalPosition - (OriginPoint + Size3DExtents);

                // save signs of axes
            float3 localPositionSign = math.sign(localPosition);

            // add 0.5f to every axis (for case of pos 15f) and ceil every axis, then abs
            float3 localPositionAbs = math.abs(math.ceil(localPosition + (new float3(0.5f) * localPositionSign)));

            // if any axes are 16f, it means they were on the edge
            if (!math.any(localPositionAbs == 16f))
            {
                return false;
            }

            // divide by 16f & floor for 0 or 1 values, apply signs, and set component normals
            normals = WydMath.ToComponents(math.floor(localPositionAbs / 16f) * localPositionSign);
            return true;
        }

        public bool TryGetBlock(float3 globalPosition, out ushort blockId)
        {
            blockId = 0;

            if (Blocks == null)
            {
#if UNITY_EDITOR

                Log.Verbose(
                    $"'{nameof(Blocks)}' is null (origin: {OriginPoint}, state: {Convert.ToString((int)State, 2)}).");

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
            State = (State & ~ChunkState.TerrainMask) | ChunkState.Detail;
        }

        private void OnTerrainDetailingFinished(object sender, AsyncJobEventArgs args)
        {
            args.AsyncJob.WorkFinished -= OnTerrainDetailingFinished;

            if (!Active)
            {
                return;
            }

            State = (State & ~ChunkState.TerrainMask) | ChunkState.TerrainComplete;
            OnLocalTerrainChanged(sender, new ChunkChangedEventArgs(OriginPoint,
                Directions.AllDirectionNormals.Select(WydMath.ToFloat)));
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
            State = (State & ~ChunkState.Meshing) | ChunkState.MeshDataPending;
        }

        #endregion
    }
}
