#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Game.World.Blocks;
using Wyd.Game.World.Chunks.Events;
using Wyd.System;
using Wyd.System.Collections;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    public class ChunkBlocksController : ActivationStateChunkController, IPerFrameIncrementalUpdate
    {
        private static readonly ObjectCache<BlockAction> _BlockActionsCache =
            new ObjectCache<BlockAction>(true, 1024);


        #region INSTANCE MEMBERS

        private Queue<BlockAction> _BlockActions;

        public Octree<ushort> Blocks { get; private set; }
        public int PendingBlockActions => _BlockActions.Count;

        #endregion


        #region SERIALIZED MEMBERS

        [SerializeField]
        [ReadOnlyInspectorField]
        private int TotalNodes = -1;

        [SerializeField]
        [ReadOnlyInspectorField]
        private uint UniqueNodes;

        [SerializeField]
        [ReadOnlyInspectorField]
        private uint NonAirBlocks;

        #endregion


        protected override void Awake()
        {
            base.Awake();

            Blocks = new Octree<ushort>(_Volume.MinPoint + (ChunkController.Size / new float3(2f)),
                ChunkController.Size.x, 0);
            _BlockActions = new Queue<BlockAction>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            PerFrameUpdateController.Current.RegisterPerFrameUpdater(30, this);
            ClearInternalData();

            Blocks = new Octree<ushort>(_Volume.MinPoint + (ChunkController.Size / new float3(2f)),
                ChunkController.Size.x, 0);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            PerFrameUpdateController.Current.DeregisterPerFrameUpdater(30, this);
            ClearInternalData();
        }

        public void FrameUpdate() { }

        public IEnumerable IncrementalFrameUpdate()
        {
            while (_BlockActions.Count > 0)
            {
                BlockAction blockAction = _BlockActions.Dequeue();

                ProcessBlockAction(blockAction);

                _BlockActionsCache.CacheItem(ref blockAction);

                yield return null;
            }
        }

        private void ProcessBlockAction(BlockAction blockAction)
        {
            ModifyBlockPosition(blockAction.GlobalPosition, blockAction.Id);

            OnBlocksChanged(this,
                TryUpdateGetNeighborNormals(blockAction.GlobalPosition, out IEnumerable<int3> normals)
                    ? new ChunkChangedEventArgs(_Volume, normals)
                    : new ChunkChangedEventArgs(_Volume, Enumerable.Empty<int3>()));
        }

        private bool TryUpdateGetNeighborNormals(float3 globalPosition, out IEnumerable<int3> normals)
        {
            normals = Enumerable.Empty<int3>();

            float3 localPosition = globalPosition - _Volume.CenterPoint;
            float3 localPositionSign = math.sign(localPosition);
            float3 localPositionAbs = math.abs(math.ceil(localPosition + (new float3(0.5f) * localPositionSign)));

            if (!math.any(localPositionAbs == 16f))
            {
                return false;
            }

            normals = WydMath.ToComponents(WydMath.ToInt(math.floor(localPositionAbs / 16f) * localPositionSign));
            return true;
        }


        #region DE/ACTIVATION

        private void ClearInternalData()
        {
            _BlockActions.Clear();
            Blocks.Collapse(true);
        }

        #endregion


        #region TRY GET / PLACE / REMOVE BLOCKS

        private void ModifyBlockPosition(int3 globalPosition, ushort newId)
        {
            if (!Blocks.ContainsPoint(globalPosition))
            {
                return;
            }

            Blocks.SetPoint(globalPosition, newId);
        }

        public ushort GetBlockAt(int3 globalPosition)
        {
            if (!Blocks.ContainsPoint(globalPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(globalPosition),
                    "Given position must be within chunk's bounds.");
            }

            return Blocks.GetPoint(globalPosition);
        }

        public bool TryGetBlockAt(int3 globalPosition, out ushort blockId)
        {
            blockId = 0;

            if (!Blocks.ContainsPoint(globalPosition))
            {
                return false;
            }

            blockId = Blocks.GetPoint(globalPosition);

            return true;
        }

        public bool BlockExistsAt(int3 globalPosition) => GetBlockAt(globalPosition) != BlockController.AIR_ID;

        public bool TryPlaceBlockAt(int3 globalPosition, ushort id) =>
            _Volume.ContainsMinBiased(globalPosition)
            && TryAllocateBlockAction(globalPosition, id);

        public bool TryRemoveBlockAt(int3 globalPosition) =>
            _Volume.ContainsMinBiased(globalPosition)
            && TryAllocateBlockAction(globalPosition, BlockController.AIR_ID);

        private bool TryAllocateBlockAction(int3 globalPosition, ushort id)
        {
            BlockAction blockAction = _BlockActionsCache.Retrieve();
            blockAction.SetData(globalPosition, id);
            _BlockActions.Enqueue(blockAction);
            return true;
        }

        #endregion


        #region EVENTS

        public event EventHandler<ChunkChangedEventArgs> BlocksChanged;

        protected virtual void OnBlocksChanged(object sender, ChunkChangedEventArgs args)
        {
            BlocksChanged?.Invoke(sender, args);
        }

        #endregion
    }
}
