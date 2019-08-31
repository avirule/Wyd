#region

using Controllers.Entity;
using Controllers.Game;
using Controllers.UI.Diagnostics;
using Controllers.World;
using Game.Entity;
using Threading.ThreadedQueue;
using UnityEngine;

#endregion

namespace Game.World
{
    public enum ThreadingMode
    {
        Single = 0,
        Multi = 1,
        Variable = 2
    }

    public class Chunk : MonoBehaviour, IEntityChunkChangedSubscriber
    {
        public static readonly ThreadedQueue ThreadedExecutionQueue = new ThreadedQueue(200, 4000);
        public static readonly Vector3Int Size = new Vector3Int(32, 256, 32);

        private Vector3 _Position;
        private ushort[] _Blocks;
        private Mesh _Mesh;
        private object _BuildingIdentity;
        private object _MeshingIdentity;

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
            Built = Building = Meshed = Meshing = PendingMeshUpdate = false;

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
            if (EntityChangedChunk)
            {
                CheckUpdateInternalSettings(PlayerController.Current.CurrentChunk);
            }

            if (Active)
            {
                GenerationCheckAndStart();
            }
        }

        private void OnApplicationQuit()
        {
            // Deallocate and destroy ALL NativeCollection / disposable objects
            ThreadedExecutionQueue.Abort();
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

        private object BeginBuildChunk()
        {
            Built = false;
            Building = true;

            return ThreadedExecutionQueue.AddThreadedItem(new ChunkBuildingThreadedItem(Position, ref _Blocks));
        }

        private object BeginGenerateMesh()
        {
            if (!Built)
            {
                return default;
            }

            Meshed = PendingMeshUpdate = false;
            Meshing = true;

            return ThreadedExecutionQueue.AddThreadedItem(new ChunkMeshingThreadedItem(Position, _Blocks));
        }

        private void GenerationCheckAndStart()
        {
            if (Building)
            {
                if (ThreadedExecutionQueue.TryGetFinishedItem(_BuildingIdentity, out ThreadedItem threadedItem))
                {
                    Building = false;
                    Built = PendingMeshUpdate = true;

                    DiagnosticsPanelController.ChunkBuildTimes.Enqueue(threadedItem.ExecutionTime.TotalMilliseconds);
                }
            }
            else if (!Built && !Building)
            {
                _BuildingIdentity = BeginBuildChunk();
            }

            if (Meshing)
            {
                if (ThreadedExecutionQueue.TryGetFinishedItem(_MeshingIdentity, out ThreadedItem threadedItem))
                {
                    Meshing = false;
                    Meshed = true;

                    ((ChunkMeshingThreadedItem) threadedItem).SetMesh(ref _Mesh);
                    MeshFilter.mesh = _Mesh;

                    DiagnosticsPanelController.ChunkMeshTimes.Enqueue(threadedItem.ExecutionTime.TotalMilliseconds);
                }
            }
            else if ((PendingMeshUpdate || !Meshed) && ChunkController.Current.AllChunksBuilt)
            {
                _MeshingIdentity = BeginGenerateMesh();
            }
        }

        #endregion

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

        private void CheckUpdateInternalSettings(Vector3Int chunkPosition)
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
    }
}