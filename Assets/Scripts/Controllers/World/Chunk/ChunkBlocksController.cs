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

        private OctreeNode _Blocks;
        private Queue<BlockAction> _BlockActions;

        public ref OctreeNode Blocks => ref _Blocks;

        public int PendingBlockActions => _BlockActions.Count;

        #endregion


        #region SERIALIZED MEMBERS

#if UNITY_EDITOR

        [SerializeField]
        [ReadOnlyInspectorField]
        private int TotalNodes = -1;

        [SerializeField]
        [ReadOnlyInspectorField]
        private uint UniqueNodes;

        [SerializeField]
        [ReadOnlyInspectorField]
        private uint NonAirBlocks;

#endif

        #endregion


        protected override void Awake()
        {
            base.Awake();

            _BlockActions = new Queue<BlockAction>();

            Blocks = new OctreeNode(OriginPoint + (ChunkController.Size / new float3(2f)),
                ChunkController.Size.x, 0);
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            PerFrameUpdateController.Current.RegisterPerFrameUpdater(30, this);
            _BlockActions.Clear();

            Blocks = new OctreeNode(OriginPoint + (ChunkController.Size / new float3(2f)),
                ChunkController.Size.x, 0);
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            PerFrameUpdateController.Current.DeregisterPerFrameUpdater(30, this);
            _BlockActions.Clear();
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
                    ? new ChunkChangedEventArgs(OriginPoint, normals)
                    : new ChunkChangedEventArgs(OriginPoint, Enumerable.Empty<int3>()));
        }

        private bool TryUpdateGetNeighborNormals(float3 globalPosition, out IEnumerable<int3> normals)
        {
            normals = Enumerable.Empty<int3>();

            float3 localPosition = globalPosition - (OriginPoint + (WydMath.ToFloat(ChunkController.Size) / 2f));
            float3 localPositionSign = math.sign(localPosition);
            float3 localPositionAbs = math.abs(math.ceil(localPosition + (new float3(0.5f) * localPositionSign)));

            if (!math.any(localPositionAbs == 16f))
            {
                return false;
            }

            normals = WydMath.ToComponents(WydMath.ToInt(math.floor(localPositionAbs / 16f) * localPositionSign));
            return true;
        }


        #region TRY GET / PLACE / REMOVE BLOCKS

        private void ModifyBlockPosition(float3 globalPosition, ushort newId)
        {
            if (!Blocks.ContainsMinBiased(globalPosition))
            {
                return;
            }

            Blocks.SetPoint(globalPosition, newId);
        }

        public ushort GetBlockAt(float3 globalPosition)
        {
            if (!Blocks.ContainsMinBiased(globalPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(globalPosition),
                    "Given position must be within chunk's bounds.");
            }

            return Blocks.GetPoint(globalPosition);
        }

        public bool TryGetBlockAt(float3 globalPosition, out ushort blockId)
        {
            blockId = 0;

            if (!Blocks.ContainsMinBiased(globalPosition))
            {
                return false;
            }

            blockId = Blocks.GetPoint(globalPosition);

            return true;
        }

        public bool BlockExistsAt(float3 globalPosition) => GetBlockAt(globalPosition) != BlockController.AIR_ID;

        public bool TryPlaceBlockAt(float3 globalPosition, ushort id) =>
            Blocks.ContainsMinBiased(globalPosition)
            && TryAllocateBlockAction(globalPosition, id);

        public bool TryRemoveBlockAt(float3 globalPosition) =>
            Blocks.ContainsMinBiased(globalPosition)
            && TryAllocateBlockAction(globalPosition, BlockController.AIR_ID);

        private bool TryAllocateBlockAction(float3 globalPosition, ushort id)
        {
            BlockAction blockAction = _BlockActionsCache.Retrieve();
            blockAction.SetData(globalPosition, id);
            _BlockActions.Enqueue(blockAction);
            return true;
        }

        #endregion


        #region EVENTS

        public event EventHandler<ChunkChangedEventArgs> BlocksChanged;

        private void OnBlocksChanged(object sender, ChunkChangedEventArgs args)
        {
            BlocksChanged?.Invoke(sender, args);
        }

        #endregion
    }
}
