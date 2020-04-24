#region

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Game;
using Wyd.Game.World.Blocks;
using Wyd.Game.World.Chunks;
using Wyd.System;
using Wyd.System.Collections;
using Wyd.System.Extensions;
using Wyd.System.Jobs;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    public enum ChunkState
    {
        Unbuilt,
        AwaitingBuilding,
        Undetailed,
        AwaitingDetailing,
        Mesh,
        AwaitingMesh,
        Meshed
    }

    public class ChunkController : ActivationStateChunkController, IPerFrameIncrementalUpdate
    {
        public const int SIZE = 32;
        public const int SIZE_SQUARED = SIZE * SIZE;
        public const int SIZE_CUBED = SIZE * SIZE * SIZE;

        private static readonly ObjectCache<BlockAction> _blockActionsCache = new ObjectCache<BlockAction>(false, 1024);

        public static readonly int3 Size3D = new int3(SIZE);
        public static readonly int3 Size3DExtents = new int3(SIZE / 2);


        #region Instance Members

        private CancellationTokenSource _CancellationTokenSource;
        private List<ChunkController> _Neighbors;
        private ConcurrentQueue<BlockAction> _BlockActions;
        private OctreeNode<ushort> _Blocks;
        private Mesh _Mesh;
        private bool _EnabledRecently;
        private long _BlockActionsCount;

        private long _State;
        private bool _Active;
        private object _ActiveLock;
        private bool _UpdateMesh;
        private object _UpdateMeshLock;

        private object _TerrainJobIdentity;
        private object _MeshJobIdentity;
        private ChunkTerrainJob _FinishedTerrainJob;
        private ChunkMeshingJob _FinishedMeshingJob;

        private bool UpdateMesh
        {
            get
            {
                bool tmp;

                lock (_UpdateMeshLock)
                {
                    tmp = _UpdateMesh;
                }

                return tmp;
            }
            set
            {
                lock (_UpdateMeshLock)
                {
                    _UpdateMesh = value;
                }
            }
        }

        public bool Active
        {
            get
            {
                bool tmp;

                lock (_ActiveLock)
                {
                    tmp = _Active;
                }

                return tmp;
            }
            set
            {
                lock (_ActiveLock)
                {
                    _Active = value;
                }
            }
        }

        public ChunkState State
        {
            get => (ChunkState)Interlocked.Read(ref _State);
            private set
            {
                Interlocked.Exchange(ref _State, (long)value);

#if UNITY_EDITOR

                InternalState = State;

#endif
            }
        }

        public OctreeNode<ushort> Blocks => _Blocks;

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
        private ChunkState InternalState;

        [SerializeField]
        private bool Regenerate;
#endif

        #endregion


        protected override void Awake()
        {
            base.Awake();

            _Neighbors = new List<ChunkController>();

            _ActiveLock = new object();
            _UpdateMeshLock = new object();

            _BlockActions = new ConcurrentQueue<BlockAction>();
            _Mesh = new Mesh();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            PerFrameUpdateController.Current.RegisterPerFrameUpdater(20, this);

            _CancellationTokenSource = new CancellationTokenSource();
            Active = gameObject.activeSelf;
            _EnabledRecently = true;

            BlocksChanged += FlagUpdateMeshCallback;
            TerrainChanged += FlagUpdateMeshCallback;

#if UNITY_EDITOR

            MinimumPoint = OriginPoint;
            MaximumPoint = OriginPoint + Size3D;

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
            _Neighbors.Clear();
            _BlockActions = new ConcurrentQueue<BlockAction>();
            _Blocks = null;

            _TerrainJobIdentity = _MeshJobIdentity = null;
            _FinishedTerrainJob = null;
            _FinishedMeshingJob = null;

            State = ChunkState.Unbuilt;
            UpdateMesh = false;
            BlocksChanged = TerrainChanged = MeshChanged = null;
            Active = gameObject.activeSelf;
        }

        private void OnDestroy()
        {
            Destroy(_Mesh);
            Destroy(TerrainController);
            Destroy(MeshController);
        }

        public void FrameUpdate()
        {
#if UNITY_EDITOR

            if (Regenerate)
            {
                State = ChunkState.Unbuilt;
                _FinishedMeshingJob = null;
                Regenerate = false;
            }

#endif

            if (_EnabledRecently)
            {
                _Neighbors.InsertRange(0, WorldController.Current.GetNeighboringChunks(OriginPoint));

                State = ChunkState.Unbuilt;

                _EnabledRecently = false;
            }

            if (((State == ChunkState.Meshed) && !UpdateMesh) || !WorldController.Current.ReadyForGeneration)
            {
                return;
            }
            else if ((State > ChunkState.Unbuilt) && (State < ChunkState.Mesh))
            {
                if (_Neighbors.Any(chunkController => chunkController.State < State))
                {
                    return;
                }
            }

            switch (State)
            {
                case ChunkState.Unbuilt:
                    TerrainController.BeginTerrainGeneration(_CancellationTokenSource.Token, OnTerrainGenerationFinished, out _TerrainJobIdentity);

                    State = State.Next();
                    break;
                case ChunkState.AwaitingBuilding:
                    if (_FinishedTerrainJob != null)
                    {
                        _FinishedTerrainJob.GetGeneratedBlockData(out _Blocks);
                        _FinishedTerrainJob = null;
                        State = State.Next();
                    }

                    break;
                case ChunkState.Undetailed:
                    TerrainController.BeginTerrainDetailing(_CancellationTokenSource.Token, OnTerrainDetailingFinished, _Blocks,
                        out _TerrainJobIdentity);

                    State = State.Next();
                    break;
                case ChunkState.AwaitingDetailing:
                    if (_FinishedTerrainJob != null)
                    {
                        _FinishedTerrainJob.GetGeneratedBlockData(out _Blocks);
                        _FinishedTerrainJob = null;
                        State = State.Next();
                        OnLocalTerrainChanged(this, new ChunkChangedEventArgs(OriginPoint, Directions.AllDirectionNormals.Select(WydMath.ToFloat)));
                    }

                    break;
                case ChunkState.Mesh:
                    if (Blocks.IsUniform
                        && ((Blocks.Value == BlockController.AirID)
                            || _Neighbors.All(chunkController => (chunkController.Blocks != null)
                                                                 && chunkController.Blocks.IsUniform)))
                    {
                        State = ChunkState.Meshed;
                    }
                    else
                    {
                        MeshController.BeginGeneratingMesh(_CancellationTokenSource.Token, OnMeshingFinished, Blocks,
                            out _MeshJobIdentity);
                        State = State.Next();
                    }

                    UpdateMesh = false;

                    break;
                case ChunkState.AwaitingMesh:
                    if (_FinishedMeshingJob != null)
                    {
                        MeshController.ApplyMesh(_FinishedMeshingJob);
                        _FinishedMeshingJob = null;
                        State = State.Next();
                        OnMeshChanged(this, new ChunkChangedEventArgs(OriginPoint, Enumerable.Empty<float3>()));
                    }

                    break;
                case ChunkState.Meshed:
                    if (UpdateMesh && _BlockActions.IsEmpty)
                    {
                        State = ChunkState.Mesh;
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public IEnumerable IncrementalFrameUpdate()
        {
            if ((State < ChunkState.Mesh) || (Interlocked.Read(ref _BlockActionsCount) == 0))
            {
                yield break;
            }

            while (_BlockActions.TryDequeue(out BlockAction blockAction))
            {
                ProcessBlockAction(blockAction);

                _blockActionsCache.CacheItem(ref blockAction);

                Interlocked.Decrement(ref _BlockActionsCount);

                yield return null;
            }
        }

        public void FlagMeshForUpdate()
        {
            if (!UpdateMesh)
            {
                UpdateMesh = true;
            }
        }


        #region De/Activation

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


        #region Block Actions

        public ushort GetBlock(float3 globalPosition) =>
            Blocks?.GetPoint(math.abs(globalPosition - OriginPoint)) ?? BlockController.NullID;

        public void PlaceBlock(float3 globalPosition, ushort newBlockId) =>
            AllocateBlockAction(math.abs(globalPosition - OriginPoint), newBlockId);

        private void AllocateBlockAction(float3 localPosition, ushort id)
        {
            if (_blockActionsCache.TryRetrieve(out BlockAction blockAction))
            {
                blockAction.SetData(localPosition, id);
            }
            else
            {
                blockAction = new BlockAction(localPosition, id);
            }

            _BlockActions.Enqueue(blockAction);

            Interlocked.Increment(ref _BlockActionsCount);
        }

        private void ProcessBlockAction(BlockAction blockAction)
        {
            Blocks.SetPoint(blockAction.LocalPosition, blockAction.Id);


            OnBlocksChanged(this,
                TryGetNeighborsRequiringUpdateNormals(blockAction.LocalPosition, out IEnumerable<float3> normals)
                    ? new ChunkChangedEventArgs(OriginPoint, normals)
                    : new ChunkChangedEventArgs(OriginPoint, Enumerable.Empty<float3>()));
        }

        private bool TryGetNeighborsRequiringUpdateNormals(float3 globalPosition, out IEnumerable<float3> normals)
        {
            normals = Enumerable.Empty<float3>();

            // set local position to center point of chunk
            float3 localPosition = globalPosition - OriginPoint - Size3DExtents;

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

        private void OnMeshChanged(object sender, ChunkChangedEventArgs args)
        {
            MeshChanged?.Invoke(sender, args);
        }


        private Task OnTerrainGenerationFinished(object sender, AsyncJobEventArgs args)
        {
            args.AsyncJob.WorkFinished -= OnTerrainGenerationFinished;

            if (!Active)
            {
                return Task.CompletedTask;
            }

            _FinishedTerrainJob = (ChunkTerrainJob)args.AsyncJob;
            return Task.CompletedTask;
        }

        private Task OnTerrainDetailingFinished(object sender, AsyncJobEventArgs args)
        {
            args.AsyncJob.WorkFinished -= OnTerrainDetailingFinished;

            if (!args.AsyncJob.Identity.Equals(_TerrainJobIdentity))
            {
                return Task.CompletedTask;
            }

            _FinishedTerrainJob = (ChunkTerrainJob)args.AsyncJob;
            return Task.CompletedTask;
        }

        private Task OnMeshingFinished(object sender, AsyncJobEventArgs args)
        {
            args.AsyncJob.WorkFinished -= OnMeshingFinished;

            if (!args.AsyncJob.Identity.Equals(_MeshJobIdentity))
            {
                ((ChunkMeshingJob)args.AsyncJob).CacheMesher();
                return Task.CompletedTask;
            }

            _FinishedMeshingJob = (ChunkMeshingJob)args.AsyncJob;
            return Task.CompletedTask;
        }

        private void FlagUpdateMeshCallback(object sender, ChunkChangedEventArgs args)
        {
            FlagMeshForUpdate();
        }

        #endregion
    }
}
