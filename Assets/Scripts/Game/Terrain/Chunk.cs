#region

using System;
using Controllers.Entity;
using Controllers.Game;
using Controllers.UI.Diagnostics;
using Controllers.World;
using Logging;
using NLog;
using Static;
using Threading;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Game.Terrain
{
    public class Chunk : MonoBehaviour
    {
        private static readonly MultiThreadedQueue BuildingQueue = new MultiThreadedQueue(200);
        private static readonly MultiThreadedQueue MeshingQueue = new MultiThreadedQueue(200);
        public static readonly Vector3Int Size = new Vector3Int(16, 256, 16);

        private object _BuildingIdentity;
        private object _MeshingIdentity;
        private Camera _MainCamera;
        private Matrix4x4 _WorldMatrix;

        public Mesh Mesh;
        public Block[] Blocks;
        public bool Built;
        public bool Building;
        public bool Meshed;
        public bool Meshing;
        public bool PendingMeshUpdate;
        public Material BlocksMaterial;
        public Vector3Int Position;

        public bool DrawShadows;

        public bool Active => gameObject.activeSelf;

        private void Awake()
        {
            if (!MeshingQueue.Running)
            {
                MeshingQueue.Start();
            }

            if (!BuildingQueue.Running)
            {
                BuildingQueue.Start();
            }

            _MainCamera = Camera.main;
            _WorldMatrix = transform.localToWorldMatrix;

            Blocks = new Block[Size.x * Size.y * Size.z];
            Position = transform.position.ToInt();
            Built = Building = Meshed = Meshing = false;
            PendingMeshUpdate = true;
            double waitTime = TimeSpan
                .FromTicks((DateTime.Now.Ticks - WorldController.Current.InitialTick) %
                           WorldController.Current.WorldTickRate.Ticks)
                .TotalSeconds;
            InvokeRepeating(nameof(Tick), (float) waitTime, (float) WorldController.Current.WorldTickRate.TotalSeconds);
        }

        private void Start()
        {
            PlayerController.Current.ChunkChanged += CheckUpdateInternalSettings;
        }

        private void LateUpdate()
        {
            if (!Built || !Meshed || Meshing)
            {
                return;
            }
            
            Graphics.DrawMesh(Mesh, _WorldMatrix, BlocksMaterial, 0, _MainCamera, 0, null, ShadowCastingMode.On, DrawShadows);
        }

        private void OnApplicationQuit()
        {
            // Deallocate and destroy ALL NativeCollection / disposable objects
            BuildingQueue.Abort();
            MeshingQueue.Abort();
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

            return BuildingQueue.AddThreadedItem(new ChunkBuildingThreadedItem(ref Blocks, noiseMap));
        }

        private object BeginGenerateMesh()
        {
            if (!Built)
            {
                return default;
            }

            Meshed = PendingMeshUpdate = false;
            Meshing = true;

            return MeshingQueue.AddThreadedItem(new ChunkMeshingThreadedItem(Position, Blocks));
        }

        public void Activate(Vector3Int position = default)
        {
            transform.position = position;
            _WorldMatrix = transform.localToWorldMatrix;
            Position = position;
            PendingMeshUpdate = true;
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            if (Mesh != default)
            {
                Mesh.Clear();
            }

            gameObject.SetActive(false);
        }

        private void Tick()
        {
            if (!Active)
            {
                return;
            }

            GenerationCheckAndStart();
        }

        private void GenerationCheckAndStart()
        {
            if (Building)
            {
                if (BuildingQueue.TryGetFinishedItem(_BuildingIdentity, out ThreadedItem threadedItem))
                {
                    Building = false;
                    Built = PendingMeshUpdate = true;

                    DiagnosticsPanelController.ChunkBuildTimes.Enqueue(threadedItem.ExecutionTime);
                }
            }
            else if (!Built && !Building)
            {
                _BuildingIdentity = BeginBuildChunk();
            }

            if (Meshing)
            {
                if (MeshingQueue.TryGetFinishedItem(_MeshingIdentity, out ThreadedItem threadedItem))
                {
                    Meshing = false;
                    Meshed = true;

                    ((ChunkMeshingThreadedItem) threadedItem).GetMesh(ref Mesh);

                    DiagnosticsPanelController.ChunkMeshTimes.Enqueue(threadedItem.ExecutionTime);
                }
            }
            else if ((PendingMeshUpdate || !Meshed) && ChunkController.Current.AllChunksBuilt)
            {
                _MeshingIdentity = BeginGenerateMesh();
            }

        }

        private void CheckUpdateInternalSettings(object sender, Vector3Int chunkPosition)
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