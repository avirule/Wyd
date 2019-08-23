#region

using System;
using System.Diagnostics;
using Controllers.Entity;
using Controllers.Game;
using Controllers.UI.Diagnostics;
using Controllers.World;
using Logging;
using NLog;
using Static;
using Threading;
using Threading.Jobs;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Environment.Terrain
{
    public class Chunk : MonoBehaviour
    {
        private static ThreadedQueue _meshingQueue = new ThreadedQueue(500);
        public static Vector3Int Size = new Vector3Int(8, 64, 16);

        private Stopwatch _BuildTimer;
        private Stopwatch _MeshTimer;
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
            if (!_meshingQueue.Running)
            {
                _meshingQueue = new ThreadedQueue(500);
                _meshingQueue.Start();
            }

            _BuildTimer = new Stopwatch();
            _MeshTimer = new Stopwatch();

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

        /// <summary>
        ///     Deallocate and destroy ALL NativeCollection / disposable objects
        /// </summary>
        private void OnApplicationQuit()
        {
            _meshingQueue.Abort();
        }

        private void Build()
        {
            _BuildTimer.Restart();

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
                _BuildTimer.Stop();
                return;
            }

            if (noiseMap == null)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to generate chunk at position ({Position.x}, {Position.z}): failed to get noise map.");
                Building = false;
                _BuildTimer.Stop();
                return;
            }

            ChunkBuilderJob chunkBuilderJob = new ChunkBuilderJob
            {
                Blocks = new NativeArray<Block>(Blocks, Allocator.TempJob),
                NoiseMap = new NativeArray<float>(noiseMap, Allocator.TempJob)
            };
            JobHandle builderJobHandle =
                chunkBuilderJob.Schedule(Blocks.Length, Blocks.Length / System.Environment.ProcessorCount);

            builderJobHandle.Complete();

            Building = false;
            Built = true;

            chunkBuilderJob.Blocks.CopyTo(Blocks);
            chunkBuilderJob.Blocks.Dispose();

            _BuildTimer.Stop();
            DiagnosticsPanelController.ChunkBuildTimes.Enqueue(_BuildTimer.Elapsed.TotalMilliseconds);
        }

        private object BeginGenerateMesh()
        {
            if (!Built)
            {
                return default;
            }

            _MeshTimer.Restart();

            Meshed = PendingMeshAssigment = false;
            Meshing = true;

            return _meshingQueue.AddThreadedItem(new ChunkMeshingThreadedItem(Position, Blocks));
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
            if (!Built && !Building)
            {
                Build();
            }

            if (ChunkController.Current.AllChunksBuilt && PendingMeshUpdate)
            {
                if (Meshing)
                {
                    if (_meshingQueue.TryGetFinishedItem(_MeshingIdentity, out ThreadedItem threadedItem))
                    {
                        Meshing = PendingMeshUpdate = false;
                        Meshed = PendingMeshAssigment = true;

                        Mesh = ((ChunkMeshingThreadedItem) threadedItem).GetMesh();

                        _MeshTimer.Stop();
                        DiagnosticsPanelController.ChunkMeshTimes.Enqueue(_MeshTimer.Elapsed.TotalMilliseconds);
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