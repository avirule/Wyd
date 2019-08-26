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

namespace Environment.Terrain
{
    public class Chunk : MonoBehaviour
    {
        private static readonly MultiThreadedQueue BuildingQueue = new MultiThreadedQueue(200);
        private static readonly MultiThreadedQueue MeshingQueue = new MultiThreadedQueue(200);
        public static readonly Vector3Int Size = new Vector3Int(8, 32, 8);

        private object _BuildingIdentity;
        private object _MeshingIdentity;

        public Mesh Mesh;
        public Block[] Blocks;
        public bool Built;
        public bool Building;
        public bool Meshed;
        public bool Meshing;
        public bool PendingMeshUpdate;
        public bool PendingMeshAssigment;
        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public MeshCollider MeshCollider;
        public Vector3Int Position;

        private bool _DrawShadows;

        public bool DrawShadows
        {
            get => _DrawShadows;
            set
            {
                if (_DrawShadows == value)
                {
                    return;
                }

                _DrawShadows = value;
                UpdateDrawShadows();
            }
        }

        private bool _ExpensiveMeshing;

        public bool ExpensiveMeshing
        {
            get => _ExpensiveMeshing;
            set
            {
                if (_ExpensiveMeshing == value)
                {
                    return;
                }

                _ExpensiveMeshing = value;
                UpdateExpensiveMeshing();
            }
        }

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

            Blocks = new Block[Size.x * Size.y * Size.z];
            Position = transform.position.ToInt();
            Built = Building = Meshed = Meshing = PendingMeshAssigment = false;
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
            if (!ExpensiveMeshing)
            {
                Graphics.DrawMesh(Mesh, transform.localToWorldMatrix, MeshRenderer.material, 0);
            }
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

            Meshed = PendingMeshAssigment = false;
            Meshing = true;

            return MeshingQueue.AddThreadedItem(new ChunkMeshingThreadedItem(Position, Blocks));
        }

        public void Activate(Vector3Int position = default)
        {
            transform.position = position;
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

            if (MeshFilter.mesh != default)
            {
                MeshFilter.mesh.Clear();
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
            if (!Built)
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
                else
                {
                    _BuildingIdentity = BeginBuildChunk();
                }
            }

            if (ChunkController.Current.AllChunksBuilt && (PendingMeshUpdate || !Meshed))
            {
                if (Meshing)
                {
                    if (MeshingQueue.TryGetFinishedItem(_MeshingIdentity, out ThreadedItem threadedItem))
                    {
                        Meshing = PendingMeshUpdate = false;
                        Meshed = PendingMeshAssigment = true;

                        ((ChunkMeshingThreadedItem) threadedItem).GetMesh(ref Mesh);

                        DiagnosticsPanelController.ChunkMeshTimes.Enqueue(threadedItem.ExecutionTime);
                    }
                }
                else
                {
                    _MeshingIdentity = BeginGenerateMesh();
                }
            }

            if (ExpensiveMeshing && PendingMeshAssigment && Meshed)
            {
                AssignMesh();
            }
        }

        private void AssignMesh()
        {
            if (!Built || !Meshed)
            {
                return;
            }

            MeshFilter.mesh = Mesh;

            if (MeshCollider != default)
            {
                MeshCollider.sharedMesh = MeshFilter.sharedMesh;
            }

            PendingMeshAssigment = false;
        }

        private void CheckUpdateInternalSettings(object sender, Vector3Int chunkPosition)
        {
            // chunk player is in should always be expensive / shadowed
            if (Position == chunkPosition)
            {
                DrawShadows = ExpensiveMeshing = true;
            }
            else
            {
                Vector3Int difference = (Position - chunkPosition).Abs();

                DrawShadows = CheckDrawShadows(difference);
                ExpensiveMeshing = CheckExpensiveMeshing(difference);
            }
        }

        private void UpdateDrawShadows()
        {
            MeshRenderer.receiveShadows = DrawShadows;
            MeshRenderer.shadowCastingMode = DrawShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        private void UpdateExpensiveMeshing()
        {
            if (MeshRenderer != default)
            {
                MeshRenderer.enabled = ExpensiveMeshing;
            }

            if (MeshCollider != default)
            {
                MeshCollider.enabled = ExpensiveMeshing;
            }
        }

        private static bool CheckDrawShadows(Vector3Int difference)
        {
            return (OptionsController.Current.ShadowDistance == 0) ||
                   ((difference.x <= (OptionsController.Current.ShadowDistance * Size.x)) &&
                    (difference.z <= (OptionsController.Current.ShadowDistance * Size.z)));
        }

        private static bool CheckExpensiveMeshing(Vector3Int difference)
        {
            return (OptionsController.Current.ExpensiveMeshingDistance == 0) ||
                   ((difference.x <= (OptionsController.Current.ExpensiveMeshingDistance * Size.x)) &&
                    (difference.z <= (OptionsController.Current.ExpensiveMeshingDistance * Size.z)));
        }
    }
}