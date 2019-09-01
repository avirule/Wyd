#region

using System.Collections.Concurrent;
using Controllers.Entity;
using Controllers.Game;
using Controllers.World;
using Game.Entity;
using Threading.ThreadedQueue;
using UnityEngine;

#endregion

namespace Game.World.Chunk
{
    public enum ThreadingMode
    {
        Single = 0,
        Multi = 1,
        Variable = 2
    }

    public class Chunk : MonoBehaviour, IEntityChunkChangedSubscriber
    {
        private static readonly ObjectCache<ChunkBuildingThreadedItem> ChunkBuildersCache =
            new ObjectCache<ChunkBuildingThreadedItem>(null, null, true);

        private static readonly ObjectCache<ChunkMeshingThreadedItem> ChunkMeshersCache =
            new ObjectCache<ChunkMeshingThreadedItem>(null, null, true);

        public static readonly ConcurrentQueue<double> BuildTimes = new ConcurrentQueue<double>();
        public static readonly ConcurrentQueue<double> MeshTimes = new ConcurrentQueue<double>();
        public static readonly ThreadedQueue ThreadedExecutionQueue = new ThreadedQueue(200, 4000);
        public static readonly Vector3Int Size = new Vector3Int(32, 256, 32);

        private Vector3 _Position;
        private ushort[] _Blocks;
        private Mesh _Mesh;
        private object _BuildingIdentity;
        private object _MeshingIdentity;
        private bool _OnBorrowedUpdateTime;

        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public bool Built;
        public bool Building;
        public bool Meshed;
        public bool Meshing;
        public bool PendingMeshUpdate;

        [Header("Graphics")] public bool DrawShadows;

        public bool Active => gameObject.activeSelf;
        public bool EntityChangedChunk { get; set; }

        public Vector3 Position
        {
            get => _Position;
            set
            {
                _Position = value;
                transform.position = _Position;
            }
        }

        private void Awake()
        {
            if (!ThreadedExecutionQueue.Running)
            {
                ThreadedExecutionQueue.Start();
            }

            _Position = transform.position;
            _Blocks = new ushort[Size.x * Size.y * Size.z];
            _OnBorrowedUpdateTime = Built = Building = Meshed = Meshing = PendingMeshUpdate = false;
            _Mesh = new Mesh();

            MeshFilter.sharedMesh = _Mesh;
            MeshRenderer.material.SetTexture(TextureController.Current.MainTex,
                TextureController.Current.TerrainTexture);

            // todo implement chunk ticks
//            double waitTime = TimeSpan
//                .FromTicks((DateTime.Now.Ticks - WorldController.Current.InitialTick) %
//                           WorldController.Current.WorldTickRate.Ticks)
//                .TotalSeconds;
//            InvokeRepeating(nameof(Tick), (float) waitTime, (float) WorldController.Current.WorldTickRate.TotalSeconds);
        }

        private void Start()
        {
            ThreadedExecutionQueue.MultiThreadedExecution =
                OptionsController.Current.ThreadingMode != ThreadingMode.Single;

            PlayerController.Current.RegisterEntityChangedSubscriber(this);
        }

        private void Update()
        {
            _OnBorrowedUpdateTime = WorldController.Current.IsOnBorrowedUpdateTime();

            if (EntityChangedChunk)
            {
                CheckUpdateInternalSettings(PlayerController.Current.CurrentChunk);
            }

            if (Active)
            {
                GenerationCheckAndStart();
            }

            CullChunkLoadTimesQueue();
        }

        private void OnDestroy()
        {
            Destroy(_Mesh);
        }

        private void OnApplicationQuit()
        {
            // Deallocate and destroy ALL NativeCollection / disposable objects
            ThreadedExecutionQueue.Abort();
        }

        private static void CullChunkLoadTimesQueue()
        {
            while (MeshTimes.Count > OptionsController.Current.MaximumChunkLoadTimeBufferSize)
            {
                MeshTimes.TryDequeue(out double _);
            }

            while (MeshTimes.Count > OptionsController.Current.MaximumChunkLoadTimeBufferSize)
            {
                MeshTimes.TryDequeue(out double _);
            }
        }

        #region ACTIVATION STATE

        public void Activate(Vector3 position = default)
        {
            Position = position;
            Built = Building = Meshed = Meshing = PendingMeshUpdate = false;
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            if (_Mesh != default)
            {
                _Mesh.Clear();
            }

            gameObject.SetActive(false);
        }

        #endregion

        #region CHUNK GENERATION

        private void GenerationCheckAndStart()
        {
            CheckBuildingOrStart();
            CheckMeshingOrStart();
        }

        private void CheckBuildingOrStart()
        {
            if (Building)
            {
                if (!_OnBorrowedUpdateTime &&
                    ThreadedExecutionQueue.TryGetFinishedItem(_BuildingIdentity, out ThreadedItem threadedItem))
                {
                    Building = false;
                    Built = PendingMeshUpdate = true;

                    BuildTimes.Enqueue(threadedItem.ExecutionTime.TotalMilliseconds);
                }
            }
            else if (!Built && !Building)
            {
                _BuildingIdentity = BeginBuildChunk();
            }
        }

        private void CheckMeshingOrStart()
        {
            if (Meshing)
            {
                if (!_OnBorrowedUpdateTime &&
                    ThreadedExecutionQueue.TryGetFinishedItem(_MeshingIdentity, out ThreadedItem threadedItem))
                {
                    Meshing = false;
                    Meshed = true;

                    ((ChunkMeshingThreadedItem) threadedItem).SetMesh(ref _Mesh);

                    MeshTimes.Enqueue(threadedItem.ExecutionTime.TotalMilliseconds);
                }
            }
            else if ((PendingMeshUpdate || !Meshed) && ChunkController.Current.AllChunksBuilt)
            {
                _MeshingIdentity = BeginGenerateMesh();
            }
        }

        private object BeginBuildChunk()
        {
            Built = false;
            Building = true;

            ChunkBuildingThreadedItem threadedItem = ChunkBuildersCache.RetrieveItem();
            threadedItem.Set(Position, _Blocks);

            return ThreadedExecutionQueue.QueueThreadedItem(threadedItem);
        }

        private object BeginGenerateMesh()
        {
            if (!Built)
            {
                return default;
            }

            Meshed = PendingMeshUpdate = false;
            Meshing = true;

            ChunkMeshingThreadedItem threadedItem = ChunkMeshersCache.RetrieveItem();
            threadedItem.Set(Position, _Blocks);

            return ThreadedExecutionQueue.QueueThreadedItem(threadedItem);
        }
        
        #endregion


        #region MISC

        public ushort GetBlockAtPosition(Vector3 position)
        {
            Vector3 localPosition = (position - Position).Abs();
            int localPosition1d = localPosition.To1D(Size);

            if (_Blocks.Length <= localPosition1d)
            {
                return default;
            }

            return _Blocks[localPosition1d];
        }

        private void CheckUpdateInternalSettings(Vector3 chunkPosition)
        {
            // chunk player is in should always be expensive / shadowed
            if (Position == chunkPosition)
            {
                DrawShadows = true;
            }
            else
            {
                Vector3 difference = (Position - chunkPosition).Abs();

                DrawShadows = CheckDrawShadows(difference);
            }
        }

        private static bool CheckDrawShadows(Vector3 difference)
        {
            return (OptionsController.Current.ShadowDistance == 0) ||
                   ((difference.x <= (OptionsController.Current.ShadowDistance * Size.x)) &&
                    (difference.z <= (OptionsController.Current.ShadowDistance * Size.z)));
        }
        
        #endregion
    }
}