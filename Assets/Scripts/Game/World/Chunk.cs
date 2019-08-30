#region

using System;
using Controllers.Entity;
using Controllers.Game;
using Controllers.UI.Diagnostics;
using Controllers.World;
using Game.Entity;
using Logging;
using NLog;
using Threading;
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
        private static readonly ThreadedQueue ThreadedExecutionQueue = new ThreadedQueue(200, 4000);
        public static readonly Vector3Int Size = new Vector3Int(16, 256, 16);

        private object _BuildingIdentity;
        private object _MeshingIdentity;
        private Mesh _Mesh;

        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public ushort[] Blocks;
        public bool Built;
        public bool Building;
        public bool Meshed;
        public bool Meshing;
        public bool PendingMeshUpdate;
        public Vector3Int Position;

        [Header("Graphics")]
        public bool DrawShadows;

        public bool Active => gameObject.activeSelf;

        public bool EntityChangedChunk { get; set; }

        private void Awake()
        {
            if (!ThreadedExecutionQueue.Running)
            {
                ThreadedExecutionQueue.Start();
            }

            Blocks = new ushort[Size.x * Size.y * Size.z];
            Position = transform.position.ToInt();
            Built = Building = Meshed = Meshing = false;
            PendingMeshUpdate = true;

            MeshRenderer.material.SetTexture(TextureController.Current.MainTex, TextureController.Current.TerrainTexture);

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
            if (OptionsController.Current.ThreadingMode == ThreadingMode.Variable)
            {
                ModifyThreadedExecutionQueueThreadingMode();
            }

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

        public void Activate(Vector3Int position = default)
        {
            Transform self = transform;
            self.position = position;
            Position = position;
            Built = Building = Meshed = Meshing = false;
            PendingMeshUpdate = true;
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
        
        private static void ModifyThreadedExecutionQueueThreadingMode()
        {
            // todo something where this isn't local const. Relative to max internal frame time maybe?
            const float fps60 = 1f / 60f;



            if (ThreadedExecutionQueue.MultiThreadedExecution && (Time.deltaTime > fps60))
            {
                ThreadedExecutionQueue.MultiThreadedExecution = false;
            }
            else if (!ThreadedExecutionQueue.MultiThreadedExecution && (Time.deltaTime <= fps60))
            {
                ThreadedExecutionQueue.MultiThreadedExecution = true;
            }
        }

        private object BeginBuildChunk()
        {
            Built = false;
            Building = true;

            float[] noiseMap;

            try
            {
                // todo fix retrieval of noise values without errors when player is moving extremely fast

                noiseMap = WorldController.Current.NoiseMap.GetSection(Position, Size);
            }
            catch (Exception)
            {
                Building = false;
                return default;
            }

            if (noiseMap == default)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to generate chunk at position ({Position.x}, {Position.z}): failed to get noise map.");
                Building = false;
                return default;
            }

            return ThreadedExecutionQueue.AddThreadedItem(new ChunkBuildingThreadedItem(ref Position, ref Blocks,
                noiseMap));
        }

        private object BeginGenerateMesh()
        {
            if (!Built)
            {
                return default;
            }

            Meshed = PendingMeshUpdate = false;
            Meshing = true;

            return ThreadedExecutionQueue.AddThreadedItem(new ChunkMeshingThreadedItem(Position, Blocks));
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

                    ((ChunkMeshingThreadedItem) threadedItem).GetMesh(ref _Mesh);
                    MeshFilter.mesh = _Mesh;

                    DiagnosticsPanelController.ChunkMeshTimes.Enqueue(threadedItem.ExecutionTime.TotalMilliseconds);
                }
            }
            else if ((PendingMeshUpdate || !Meshed) && ChunkController.Current.AllChunksBuilt)
            {
                _MeshingIdentity = BeginGenerateMesh();
            }
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
                Vector3Int difference = (Position - chunkPosition).Abs();

                DrawShadows = CheckDrawShadows(difference);
            }
        }

        private static bool CheckDrawShadows(Vector3Int difference)
        {
            return (OptionsController.Current.ShadowDistance == 0) ||
                   ((difference.x <= (OptionsController.Current.ShadowDistance * Size.x)) &&
                    (difference.z <= (OptionsController.Current.ShadowDistance * Size.z)));
        }
    }
}