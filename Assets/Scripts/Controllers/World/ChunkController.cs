#region

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Serilog;
using UnityEngine;
using UnityEngine.Rendering;
using Wyd.Controllers.State;
using Wyd.Game;
using Wyd.Game.Entities;
using Wyd.Game.World.Blocks;
using Wyd.Game.World.Chunks;
using Wyd.System;
using Wyd.System.Collections;
using Wyd.System.Compression;

#endregion

namespace Wyd.Controllers.World
{
    public class ChunkController : MonoBehaviour
    {
        public static readonly Vector3Int Size = new Vector3Int(32, 256, 32);
        public static readonly int SizeProduct = Size.Product();
        public static readonly int YIndexStep = Size.x * Size.z;

        private static readonly ObjectCache<ChunkGenerator> ChunkGeneratorsCache =
            // todo decide how to handle this cache's size
            new ObjectCache<ChunkGenerator>(true, false, (Size.x / 2) * (Size.z / 2));

        private static readonly ObjectCache<BlockAction> BlockActionsCache =
            new ObjectCache<BlockAction>(true, true, 1024);


        #region INSTANCE MEMBERS

        private Bounds _Bounds;
        private Transform _SelfTransform;
        private IEntity _CurrentLoader;
        private LinkedList<RLENode<ushort>> _Blocks;
        private Mesh _Mesh;
        private bool _Visible;
        private bool _RenderShadows;
        private ChunkGenerator _ChunkGenerator;
        private Stack<BlockAction> _BlockActions;
        private HashSet<Vector3> _BlockActionLocalPositions;

        [SerializeField]
        private MeshFilter MeshFilter;

        [SerializeField]
        private MeshRenderer MeshRenderer;

        public Vector3 Position => _Bounds.min;
        public ChunkGenerator.GenerationStep GenerationStep => _ChunkGenerator.CurrentStep;

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


        #region UNITY BUILT-INS

        private void Awake()
        {
            _SelfTransform = transform;
            UpdateBounds();
            _Blocks = new LinkedList<RLENode<ushort>>();
            _Mesh = new Mesh();
            _BlockActions = new Stack<BlockAction>();
            _BlockActionLocalPositions = new HashSet<Vector3>();

            MeshFilter.sharedMesh = _Mesh;

            foreach (Material material in MeshRenderer.materials)
            {
                material.SetTexture(TextureController.MainTexPropertyID, TextureController.Current.TerrainTexture);
            }

            _Visible = MeshRenderer.enabled;

            ConfigureGenerator();

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

        private void Update()
        {
            if (!WorldController.Current.IsInSafeFrameTime())
            {
                return;
            }

            if (_BlockActions.Count > 0)
            {
                ProcessBlockActions();
            }
            else if (WorldController.Current.ReadyForGeneration)
            {
                _ChunkGenerator.SynchronousContextUpdate();
            }
        }

        private void OnDestroy()
        {
            Destroy(_Mesh);
            OnDestroyed(this, new ChunkChangedEventArgs(_Bounds, Directions.CardinalDirectionsVector3));
        }

        #endregion

        private void ConfigureGenerator()
        {
            _ChunkGenerator = ChunkGeneratorsCache.RetrieveItem() ?? new ChunkGenerator();
            _ChunkGenerator.Set(_Bounds, ref _Blocks, ref _Mesh);
            _ChunkGenerator.BlocksChanged += OnBlocksChanged;
            _ChunkGenerator.MeshChanged += OnMeshChanged;
        }


        private void CacheGenerator()
        {
            _ChunkGenerator.BlocksChanged -= OnBlocksChanged;
            _ChunkGenerator.MeshChanged -= OnMeshChanged;
            _ChunkGenerator.Unset();
            ChunkGeneratorsCache.CacheItem(ref _ChunkGenerator);
        }

        public void RequestMeshUpdate()
        {
            _ChunkGenerator.RequestMeshUpdate();
        }

        private void ProcessBlockActions()
        {
            while ((_BlockActions.Count > 0) && WorldController.Current.IsInSafeFrameTime())
            {
                BlockAction blockAction = _BlockActions.Pop();
                _BlockActionLocalPositions.Remove(blockAction.LocalPosition);
                int localPosition1d = blockAction.LocalPosition.To1D(Size);

                if (localPosition1d < SizeProduct)
                {
                    // todo optimize this to order the block actions by position, so modifications can happen in one pass
                    ModifyBlockPosition(localPosition1d, blockAction.Id);
                    RequestMeshUpdate();
                    OnBlocksChanged(this,
                        new ChunkChangedEventArgs(_Bounds,
                            DetermineDirectionsForNeighborUpdate(blockAction.LocalPosition)));
                }

                BlockActionsCache.CacheItem(ref blockAction);
            }
        }

        #region ACTIVATION STATE

        public void Activate(Vector3 position)
        {
            _SelfTransform.position = position;
            UpdateBounds();
            _Visible = MeshRenderer.enabled;
            ConfigureGenerator();
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            if (_Mesh != default)
            {
                _Mesh.Clear();
            }

            if (_CurrentLoader != default)
            {
                _CurrentLoader.ChunkPositionChanged -= OnCurrentLoaderChangedChunk;
                _CurrentLoader = default;
            }

            _BlockActions.Clear();
            _BlockActionLocalPositions.Clear();
            _Bounds = default;

            CacheGenerator();
            StopAllCoroutines();
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


        #region TRY GET / PLACE / REMOVE BLOCKS

        private void ModifyBlockPosition(int localPosition1d, ushort newId)
        {
            if (localPosition1d >= SizeProduct)
            {
                return;
            }

            int steppedLength = 0;

            LinkedListNode<RLENode<ushort>> currentNode = _Blocks.First;

            while ((steppedLength <= localPosition1d) && (currentNode != null))
            {
                if (currentNode.Value.RunLength == 0)
                {
                    _Blocks.Remove(currentNode);
                }
                else
                {
                    int newSteppedLength = currentNode.Value.RunLength + steppedLength;

                    // in this case, the position exists at the beginning of a node
                    if (localPosition1d == (steppedLength + 1))
                    {
                        // insert node before current node
                        _Blocks.AddBefore(currentNode, new RLENode<ushort>(1, newId));
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
                            _Blocks.AddAfter(currentNode, new RLENode<ushort>(1, newId));
                        // split CurrentNode's RunLength and -1 to make space for the inserted node
                        currentNode.Value.RunLength = localPosition1d - steppedLength - 1;

                        if (localPosition1d != newSteppedLength)
                        {
                            // hit is not at end of rle node, so we must add a remainder node
                            _Blocks.AddAfter(insertedNode,
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

            int localPosition1d = (globalPosition - Position).To1D(Size, true);

            int totalPositions = 0;
            LinkedListNode<RLENode<ushort>> currentNode = _Blocks.First;

            while ((totalPositions <= localPosition1d) && (currentNode != null))
            {
                int newTotal = currentNode.Value.RunLength + totalPositions;

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

            int localPosition1d = (globalPosition - Position).To1D(Size, true);

            int totalPositions = 0;
            LinkedListNode<RLENode<ushort>> currentNode = _Blocks.First;

            while ((totalPositions <= localPosition1d) && (currentNode != null))
            {
                int newTotal = currentNode.Value.RunLength + totalPositions;

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

        public bool BlockExistsAt(Vector3 globalPosition) => GetBlockAt(globalPosition) != BlockController.Air.Id;

        public bool TryPlaceBlockAt(Vector3 globalPosition, ushort id) =>
            _Bounds.Contains(globalPosition)
            && TryAllocateBlockAction(globalPosition - Position, id);

        public bool TryRemoveBlockAt(Vector3 globalPosition) =>
            _Bounds.Contains(globalPosition)
            && TryAllocateBlockAction(globalPosition - Position, BlockController.Air.Id);

        private bool TryAllocateBlockAction(Vector3 localPosition, ushort id)
        {
            // todo this vvvv
            if (_BlockActionLocalPositions.Contains(localPosition)
                || !_Bounds.Contains(localPosition))
            {
                return false;
            }

            BlockAction blockAction = BlockActionsCache.RetrieveItem();
            blockAction.Initialise(localPosition, id);
            _BlockActions.Push(blockAction);
            _BlockActionLocalPositions.Add(localPosition);
            return true;
        }

        #endregion


        #region HELPER METHODS

        private void UpdateBounds()
        {
            Vector3 position = _SelfTransform.position;
            _Bounds.SetMinMax(position, position + Size);
        }

        [SuppressMessage("ReSharper", "InconsistentNaming")]
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
            byte[] bytes = new byte[_Blocks.Count * 4];

            int count = 0;
            foreach (RLENode<ushort> node in _Blocks)
            {
                // copy runlength (ushort, 2 bytes) to position of i
                Array.Copy(BitConverter.GetBytes(node.RunLength), 0, bytes, count, 2);
                // copy node value, also 2 bytes, to position of i + 2 bytes from runlength
                Array.Copy(BitConverter.GetBytes(node.Value), 0, bytes, count + 2, 2);

                count += 4;
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
                // todo fix this to work with linked list
                ushort runLength = BitConverter.ToUInt16(data, i);
                ushort value = BitConverter.ToUInt16(data, i + 2);

                for (int run = 0; run < runLength; run++)
                {
                    //_Blocks[blocksIndex].Initialise(value);
                    blocksIndex += 1;
                }
            }

            _ChunkGenerator.SkipBuilding(true);
        }

        private IEnumerable<ushort> GetDecompressed() => RunLengthCompression.Decompress(_Blocks);

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

        public event EventHandler<ChunkChangedEventArgs> BlocksChanged;
        public event EventHandler<ChunkChangedEventArgs> MeshChanged;
        public event EventHandler<ChunkChangedEventArgs> DeactivationCallback;
        public event EventHandler<ChunkChangedEventArgs> Destroyed;

        protected virtual void OnBlocksChanged(object sender, ChunkChangedEventArgs args)
        {
            BlocksChanged?.Invoke(sender, args);
        }

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
