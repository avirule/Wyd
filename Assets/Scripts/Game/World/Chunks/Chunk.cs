#region

using System;
using System.Collections.Generic;
using System.Linq;
using Compression;
using Controllers.State;
using Controllers.World;
using Game.World.Blocks;
using Logging;
using NLog;
using UnityEngine;

#endregion

namespace Game.World.Chunks
{
    public class Chunk
    {
        private static readonly ObjectCache<ChunkGenerationDispatcher> ChunkGenerationDispatcherCache =
            new ObjectCache<ChunkGenerationDispatcher>(true);

        private static readonly ObjectCache<BlockAction> BlockActionsCache =
            new ObjectCache<BlockAction>(true, true, 1024);

        public static readonly Vector3Int Size = new Vector3Int(32, 32, 32);
        public static readonly int YIndexStep = Size.x * Size.z;


        #region INSTANCE MEMBERS

        private readonly Block[] _Blocks;
        private Bounds _Bounds;
        private MeshData _MeshData;
        private ChunkGenerationDispatcher _ChunkGenerationDispatcher;
        private readonly Stack<BlockAction> _BlockActions;
        private readonly HashSet<Vector3> _BlockActionLocalPositions;
        private TimeSpan _SafeFrameTime; // this value changes often, try to don't set it

        public Vector3 Position => _Bounds.min;
        public MeshData MeshData => _MeshData;
        public bool Active { get; private set; }

        public ChunkGenerationDispatcher.GenerationStep GenerationStep => _ChunkGenerationDispatcher.CurrentStep;

        #endregion


        public Chunk(Vector3 position)
        {
            _Bounds.SetMinMax(position, position + Size);
            _Blocks = new Block[Size.Product()];
            _BlockActions = new Stack<BlockAction>();
            _BlockActionLocalPositions = new HashSet<Vector3>();
            _MeshData = new MeshData();
            ConfigureDispatcher();
            Active = true;

            // todo implement chunk ticks
            // double waitTime = TimeSpan
            //     .FromTicks((DateTime.Now.Ticks - WorldController.Current.InitialTick) %
            //                WorldController.Current.WorldTickRate.Ticks)
            //     .TotalSeconds;
            // InvokeRepeating(nameof(Tick), (float) waitTime, (float) WorldController.Current.WorldTickRate.TotalSeconds);
        }

        #region UNITY BUILT-INS

        public void Update()
        {
                if (!ProcessBlockActions())
            {
                _ChunkGenerationDispatcher.SynchronousContextUpdate();
            }
        }

        #endregion


        private bool ProcessBlockActions()
        {
            if (_BlockActions.Count > 0)
            {
                do
                {
                    WorldController.Current.GetRemainingSafeFrameTime(out _SafeFrameTime);

                    BlockAction blockAction = _BlockActions.Pop();
                    _BlockActionLocalPositions.Remove(blockAction.LocalPosition);
                    int localPosition1d = blockAction.LocalPosition.To1D(Size);

                    if (localPosition1d < _Blocks.Length)
                    {
                        _Blocks[localPosition1d].Initialise(blockAction.Id);
                        RequestMeshUpdate();
                        OnBlocksChanged(this,
                            new ChunkChangedEventArgs(_Bounds,
                                DetermineDirectionsForNeighborUpdate(blockAction.LocalPosition)));
                    }

                    BlockActionsCache.CacheItem(ref blockAction);
                } while ((_BlockActions.Count > 0) && (_SafeFrameTime > TimeSpan.Zero));

                return true;
            }

            return false;
        }

        public void RequestMeshUpdate()
        {
            _ChunkGenerationDispatcher.RequestMeshUpdate();
        }


        #region ACTIVATION STATE

        public void Activate(Vector3 position)
        {
            _Bounds.SetMinMax(position, position + Size);
            ConfigureDispatcher();
            Active = true;
        }

        public void Deactivate()
        {
            if (_ChunkGenerationDispatcher != default)
            {
                CacheDispatcher();
            }

            _MeshData?.Clear();
            _BlockActions?.Clear();
            _BlockActionLocalPositions?.Clear();
            _SafeFrameTime = TimeSpan.Zero;
            _Bounds = default;

            Active = false;
        }

        private void ConfigureDispatcher()
        {
            if (!ChunkGenerationDispatcherCache.TryRetrieveItem(out _ChunkGenerationDispatcher))
            {
                EventLog.Logger.Log(LogLevel.Warn,
                    $"Chunk at position {Position} unable to retrieve a ChunkGenerationDispatcher from cache. This is most likely a serious error.");
            }

            _ChunkGenerationDispatcher.Set(_Bounds, _Blocks, ref _MeshData);
            _ChunkGenerationDispatcher.BlocksChanged += OnBlocksChanged;
            _ChunkGenerationDispatcher.MeshChanged += OnMeshChanged;
        }

        private void CacheDispatcher()
        {
            _ChunkGenerationDispatcher.BlocksChanged -= OnBlocksChanged;
            _ChunkGenerationDispatcher.MeshChanged -= OnMeshChanged;
            _ChunkGenerationDispatcher.Unset();
            ChunkGenerationDispatcherCache.CacheItem(ref _ChunkGenerationDispatcher);
        }

        #endregion


        #region TRY GET / PLACE / REMOVE BLOCKS

        public ref Block GetBlockAt(Vector3 localPosition)
        {
            int localPosition1d = localPosition.To1D(Size);

            if (localPosition1d >= _Blocks.Length)
            {
                throw new ArgumentException($"Parameter `{nameof(localPosition)}` must be within bounds.");
            }

            return ref _Blocks[localPosition1d];
        }

        public bool TryGetBlockAt(Vector3 localPosition, out Block block)
        {
            int localPosition1d = localPosition.To1D(Size);

            if (localPosition1d >= _Blocks.Length)
            {
                block = default;
                return false;
            }

            block = _Blocks[localPosition1d];
            return true;
        }

        public bool BlockExistsAt(Vector3 localPosition)
        {
            int localPosition1d = localPosition.To1D(Size);

            if (localPosition1d >= _Blocks.Length)
            {
                throw new ArgumentException($"Parameter `{nameof(localPosition)}` must be within bounds.");
            }

            return _Blocks[localPosition1d].Id != BlockController.BLOCK_EMPTY_ID;
        }

        public void ImmediatePlaceBlockAt(Vector3 localPosition, ushort id)
        {
            int localPosition1d = localPosition.To1D(Size);

            if (localPosition1d >= _Blocks.Length)
            {
                throw new ArgumentException($"Parameter `{nameof(localPosition)}` must be within bounds.");
            }

            _Blocks[localPosition1d].Initialise(id);
            RequestMeshUpdate();

            OnBlocksChanged(this,
                new ChunkChangedEventArgs(_Bounds, DetermineDirectionsForNeighborUpdate(localPosition)));
        }

        public bool TryPlaceBlockAt(Vector3 globalPosition, ushort id) => TryAllocateBlockAction(globalPosition, id);

        public void ImmediateRemoveBlockAt(Vector3 localPosition)
        {
            int localPosition1d = localPosition.To1D(Size);

            if (localPosition1d >= _Blocks.Length)
            {
                throw new ArgumentException($"Parameter `{nameof(localPosition)}` must be within bounds.");
            }

            _Blocks[localPosition1d].Initialise(BlockController.BLOCK_EMPTY_ID);
            RequestMeshUpdate();

            OnBlocksChanged(this,
                new ChunkChangedEventArgs(_Bounds, DetermineDirectionsForNeighborUpdate(localPosition)));
        }

        public bool TryRemoveBlockAt(Vector3 globalPosition) =>
            TryAllocateBlockAction(globalPosition, BlockController.BLOCK_EMPTY_ID);

        private bool TryAllocateBlockAction(Vector3 localPosition, ushort id)
        {
            // todo this vvvv
            if (_BlockActionLocalPositions.Contains(localPosition)
                || !_Bounds.Contains(localPosition)
                || !BlockActionsCache.TryRetrieveItem(out BlockAction blockAction))
            {
                return false;
            }

            blockAction.Initialise(localPosition, id);
            _BlockActions.Push(blockAction);
            _BlockActionLocalPositions.Add(localPosition);
            return true;
        }

        #endregion


        #region HELPER METHODS

        private static IEnumerable<Vector3> DetermineDirectionsForNeighborUpdate(Vector3 localPosition)
        {
            // topleft & bottomright x computation value
            float tl_br_x = localPosition.x * Size.x;
            // topleft & bottomright y computation value
            float tl_br_y = localPosition.z * Size.z;

            // topright & bottomleft left-side computation value
            float tr_bl_l = localPosition.x + localPosition.z;
            // topright & bottomleft right-side computation value
            float tr_bl_r = (Size.x + Size.z) / 2f;

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

        public byte[] GetCompressedAsByteArray()
        {
            // 4 bytes for runlength and value
            List<RunLengthCompression.Node<ushort>> nodes = GetCompressedRaw().ToList();
            byte[] bytes = new byte[nodes.Count * 4];

            for (int i = 0; i < bytes.Length; i += 4)
            {
                int nodesIndex = i / 4;

                // copy runlength (ushort, 2 bytes) to position of i
                Array.Copy(BitConverter.GetBytes(nodes[nodesIndex].RunLength), 0, bytes, i, 2);
                // copy node value, also 2 bytes, to position of i + 2 bytes from runlength
                Array.Copy(BitConverter.GetBytes(nodes[nodesIndex].Value), 0, bytes, i + 2, 2);
            }

            return bytes;
        }

        public void BuildFromByteData(byte[] data)
        {
            if ((data.Length % 4) != 0)
            {
                return;
            }

            int blocksIndex = 0;
            for (int i = 0; i < data.Length; i += 4)
            {
                ushort runLength = BitConverter.ToUInt16(data, i);
                ushort value = BitConverter.ToUInt16(data, i + 2);

                for (int run = 0; run < runLength; run++)
                {
                    _Blocks[blocksIndex].Initialise(value);
                    blocksIndex += 1;
                }
            }

            _ChunkGenerationDispatcher.SkipBuilding(true);
        }

        public IEnumerable<RunLengthCompression.Node<ushort>> GetCompressedRaw() =>
            RunLengthCompression.Compress(GetBlocksAsIds(), _Blocks[0].Id);

        private IEnumerable<ushort> GetBlocksAsIds()
        {
            return _Blocks.Select(block => block.Id);
        }

        #endregion


        #region EVENTS

        // todo chunk load failed event

        public event EventHandler<ChunkChangedEventArgs> BlocksChanged;
        public event EventHandler<ChunkChangedEventArgs> MeshChanged;

        protected virtual void OnBlocksChanged(object sender, ChunkChangedEventArgs args)
        {
            BlocksChanged?.Invoke(sender, args);
        }

        protected virtual void OnMeshChanged(object sender, ChunkChangedEventArgs args)
        {
            MeshChanged?.Invoke(sender, args);
        }

        #endregion
    }
}
