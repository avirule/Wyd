#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Unity.Mathematics;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.System;
using Wyd.Game;
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
        public int QueuedBlockActions => _BlockActions.Count;

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

            Blocks = new Octree<ushort>(_Bounds.MinPoint + (ChunkController.Size / new int3(2)), ChunkController.Size.x,
                0);
            _BlockActions = new Queue<BlockAction>();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            PerFrameUpdateController.Current.RegisterPerFrameUpdater(30, this);
            ClearInternalData();
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
            if (_BlockActions.Count > 0)
            {
                yield return ProcessBlockActions();
            }
        }

        // private void UpdateInternalStateInfo()
        // {
        //     TotalNodes = Blocks.Count;
        //
        //     HashSet<ushort> uniqueBlockIds = new HashSet<ushort>();
        //     NonAirBlocks = 0;
        //
        //     foreach (RLENode<ushort> node in Blocks)
        //     {
        //         if (!uniqueBlockIds.Contains(node.Value))
        //         {
        //             uniqueBlockIds.Add(node.Value);
        //         }
        //
        //         if (node.Value != 0)
        //         {
        //             NonAirBlocks += node.RunLength;
        //         }
        //     }
        //
        //     UniqueNodes = (uint)uniqueBlockIds.Count;
        // }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static IEnumerable<Vector3> DetermineDirectionsForNeighborUpdate(Vector3 localPosition)
        {
            // topleft & bottomright x computation value
            float tl_br_x = localPosition.x * ChunkController.Size.x;
            // topleft & bottomright y computation value
            float tl_br_y = localPosition.z * ChunkController.Size.z;

            // topright & bottomleft left-side computation value
            float tr_bl_l = localPosition.x + localPosition.z;
            // topright & bottomleft right-side computation value
            float tr_bl_r = (ChunkController.Size.x + ChunkController.Size.z) / 2f;

            // `half` refers to the diagonal half of the chunk the point lies in.
            // If the point does not lie in a diagonal half, its a center block, and we don't need to update chunks.

            bool isInTopLeftHalf = tl_br_x > tl_br_y;
            bool isInBottomRightHalf = tl_br_x < tl_br_y;
            bool isInTopRightHalf = tr_bl_l > tr_bl_r;
            bool isInBottomLeftHalf = tr_bl_l < tr_bl_r;

            if (isInTopRightHalf && isInTopLeftHalf)
            {
                yield return Vector3.right;
            }
            else if (isInTopRightHalf && isInBottomRightHalf)
            {
                yield return Vector3.forward;
            }
            else if (isInBottomRightHalf && isInBottomLeftHalf)
            {
                yield return Vector3.left;
            }
            else if (isInBottomLeftHalf && isInTopLeftHalf)
            {
                yield return Vector3.back;
            }
            else if (!isInTopRightHalf && !isInBottomLeftHalf)
            {
                if (isInTopLeftHalf)
                {
                    yield return Vector3.back;
                    yield return Vector3.left;
                }
                else if (isInBottomRightHalf)
                {
                    yield return Vector3.back;
                    yield return Vector3.right;
                }
            }
            else if (!isInTopLeftHalf && !isInBottomRightHalf)
            {
                if (isInTopRightHalf)
                {
                    yield return Vector3.forward;
                    yield return Vector3.right;
                }
                else if (isInBottomLeftHalf)
                {
                    yield return Vector3.forward;
                    yield return Vector3.left;
                }
            }
        }


        #region DE/ACTIVATION

        private void ClearInternalData()
        {
            _BlockActions.Clear();
            Blocks.Collapse(true);
        }

        #endregion


        #region TRY GET / PLACE / REMOVE BLOCKS

        private IEnumerable ProcessBlockActions()
        {
            while (_BlockActions.Count > 0)
            {
                BlockAction blockAction = _BlockActions.Dequeue();

                ModifyBlockPosition(blockAction.GlobalPosition, blockAction.Id);
                OnBlocksChanged(this, new ChunkChangedEventArgs(_Bounds, Directions.AllDirectionAxes));

                _BlockActionsCache.CacheItem(ref blockAction);

                yield return null;
            }
        }

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
            _Bounds.Contains(globalPosition)
            && TryAllocateBlockAction(globalPosition - WydMath.ToInt(_Bounds.MinPoint), id);

        public bool TryRemoveBlockAt(int3 globalPosition) =>
            _Bounds.Contains(globalPosition)
            && TryAllocateBlockAction(globalPosition - WydMath.ToInt(_Bounds.MinPoint), BlockController.AIR_ID);

        private bool TryAllocateBlockAction(int3 localPosition, ushort id)
        {
            BlockAction blockAction = _BlockActionsCache.Retrieve();
            blockAction.SetData(localPosition, id);
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
