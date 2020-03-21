#region

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Game.World.Blocks;
using Wyd.Game.World.Chunks.Events;
using Wyd.System;
using Wyd.System.Collections;
using Wyd.System.Compression;

#endregion

namespace Wyd.Controllers.World.Chunk
{
    public class ChunkBlocksController : ActivationStateChunkController
    {
        private static readonly ObjectCache<BlockAction> _BlockActionsCache =
            new ObjectCache<BlockAction>(true, true, 1024);


        #region INSTANCE MEMBERS

        private Queue<BlockAction> _BlockActions;

        public LinkedList<RLENode<ushort>> Blocks { get; private set; }
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

            Blocks = new LinkedList<RLENode<ushort>>();
            _BlockActions = new Queue<BlockAction>();
        }


        public void Update()
        {
            // if we've passed safe frame time for target
            // fps, then skip updates as necessary to reach
            // next frame
            if (!WorldController.Current.IsInSafeFrameTime())
            {
                return;
            }

            if (_BlockActions.Count > 0)
            {
                ProcessBlockActions();
            }

            if (Blocks.Count != TotalNodes)
            {
                UpdateInternalStateInfo();
            }
        }

        private void UpdateInternalStateInfo()
        {
            TotalNodes = Blocks.Count;

            HashSet<ushort> uniqueBlockIds = new HashSet<ushort>();
            NonAirBlocks = 0;

            foreach (RLENode<ushort> node in Blocks)
            {
                if (!uniqueBlockIds.Contains(node.Value))
                {
                    uniqueBlockIds.Add(node.Value);
                }

                if (node.Value != 0)
                {
                    NonAirBlocks += node.RunLength;
                }
            }

            UniqueNodes = (uint)uniqueBlockIds.Count;
        }

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

        public override void Activate(Vector3 position, bool setPosition)
        {
            base.Activate(position, setPosition);
            ClearInternalData();
        }

        public override void Deactivate()
        {
            base.Deactivate();
            ClearInternalData();
        }

        private void ClearInternalData()
        {
            _BlockActions.Clear();
            Blocks.Clear();
        }

        #endregion


        #region TRY GET / PLACE / REMOVE BLOCKS

        /// <summary>
        ///     todo write comment explaining this
        /// </summary>
        private void ProcessBlockActions()
        {
            while ((_BlockActions.Count > 0) && WorldController.Current.IsInSafeFrameTime())
            {
                BlockAction blockAction = _BlockActions.Dequeue();
                uint localPosition1d = (uint)blockAction.LocalPosition.To1D(ChunkController.Size);

                if (localPosition1d < ChunkController.SizeProduct)
                {
                    // todo optimize this to order the block actions by position, so modifications can happen in one pass
                    ModifyBlockPosition(localPosition1d, blockAction.Id);
                    OnBlocksChanged(this,
                        new ChunkChangedEventArgs(_Bounds,
                            DetermineDirectionsForNeighborUpdate(blockAction.LocalPosition)));
                }

                _BlockActionsCache.CacheItem(ref blockAction);
            }
        }

        private void ModifyBlockPosition(uint localPosition1d, ushort newId)
        {
            if (localPosition1d >= ChunkController.SizeProduct)
            {
                return;
            }

            uint steppedLength = 0;

            LinkedListNode<RLENode<ushort>> currentNode = Blocks.First;

            while ((steppedLength <= localPosition1d) && (currentNode != null))
            {
                if (currentNode.Value.RunLength == 0)
                {
                    Blocks.Remove(currentNode);
                }
                else
                {
                    uint newSteppedLength = currentNode.Value.RunLength + steppedLength;

                    // in this case, the position exists at the beginning of a node
                    if (localPosition1d == (steppedLength + 1))
                    {
                        // insert node before current node
                        Blocks.AddBefore(currentNode, new RLENode<ushort>(1, newId));
                        // decrement current node to make room for new node
                        currentNode.Value.RunLength -= 1;
                    }
                    // position exists after end of node
                    else if ((localPosition1d == (newSteppedLength + 1))
                             // and ids match so just increment RunLength without any insertions
                             && (newId == currentNode.Value.Value))
                    {
                        currentNode.Value.RunLength += 1;

                        // make sure next node is not null
                        if (currentNode.Next != null)
                        {
                            // decrement from the next node so that there's space for the new placement
                            // in currentNode's RunLength
                            currentNode.Next.Value.RunLength -= 1;
                        }
                    }
                    // we've found the node that overlaps the queried position
                    else if (newSteppedLength > localPosition1d)
                    {
                        if (currentNode.Value.Value == newId)
                        {
                            // position resulted in already exists block id
                            break;
                        }

                        // inserted node will take up 1 position / run length
                        LinkedListNode<RLENode<ushort>> insertedNode =
                            Blocks.AddAfter(currentNode, new RLENode<ushort>(1, newId));
                        // split CurrentNode's RunLength and -1 to make space for the inserted node
                        currentNode.Value.RunLength = localPosition1d - steppedLength - 1;

                        if (localPosition1d != newSteppedLength)
                        {
                            // hit is not at end of rle node, so we must add a remainder node
                            Blocks.AddAfter(insertedNode,
                                new RLENode<ushort>(newSteppedLength - localPosition1d, currentNode.Value.Value));
                        }

                        break;
                    }

                    steppedLength = newSteppedLength;
                }

                currentNode = currentNode.Next;
            }
        }

        public ushort GetBlockAt(Vector3 globalPosition)
        {
            if (!_Bounds.Contains(globalPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(globalPosition),
                    "Given position must be within chunk's bounds.");
            }

            int localPosition1d = (globalPosition - _Position).To1D(ChunkController.Size, true);

            uint totalPositions = 0;
            LinkedListNode<RLENode<ushort>> currentNode = Blocks.First;

            while ((totalPositions <= localPosition1d) && (currentNode != null))
            {
                uint newTotal = currentNode.Value.RunLength + totalPositions;

                if (newTotal >= localPosition1d)
                {
                    return currentNode.Value.Value;
                }

                totalPositions = newTotal;
                currentNode = currentNode.Next;
            }

            return 0;
        }

        public bool TryGetBlockAt(Vector3 globalPosition, out ushort blockId)
        {
            blockId = 0;

            if (!_Bounds.Contains(globalPosition))
            {
                return false;
            }

            int localPosition1d = (globalPosition - _Position).To1D(ChunkController.Size, true);

            uint totalPositions = 0;
            LinkedListNode<RLENode<ushort>> currentNode = Blocks.First;

            while ((totalPositions <= localPosition1d) && (currentNode != null))
            {
                uint newTotal = currentNode.Value.RunLength + totalPositions;

                if (newTotal >= localPosition1d)
                {
                    blockId = currentNode.Value.Value;
                    return true;
                }

                totalPositions = newTotal;
                currentNode = currentNode.Next;
            }

            return false;
        }

        public bool BlockExistsAt(Vector3 globalPosition) => GetBlockAt(globalPosition) != BlockController.AIR_ID;

        public bool TryPlaceBlockAt(Vector3 globalPosition, ushort id) =>
            _Bounds.Contains(globalPosition)
            && TryAllocateBlockAction(globalPosition - _Position, id);

        public bool TryRemoveBlockAt(Vector3 globalPosition) =>
            _Bounds.Contains(globalPosition)
            && TryAllocateBlockAction(globalPosition - _Position, BlockController.AIR_ID);

        private bool TryAllocateBlockAction(Vector3 localPosition, ushort id)
        {
            BlockAction blockAction = _BlockActionsCache.RetrieveItem();
            blockAction.Initialise(localPosition, id);
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
