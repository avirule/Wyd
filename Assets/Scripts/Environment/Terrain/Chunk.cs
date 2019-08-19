#region

using System;
using System.Diagnostics;
using Controllers.Entity;
using Controllers.Game;
using Controllers.UI.Diagnostics;
using Controllers.World;
using Environment.Terrain.Generation;
using Logging;
using NLog;
using Static;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Environment.Terrain
{
    public class Chunk : MonoBehaviour
    {
        public static Vector3Int Size = new Vector3Int(16, 16, 16);

        private Stopwatch _BuildTimer;
        private Stopwatch _MeshTimer;
        private ChunkBuilderJob _ChunkBuilderJob;
        private ChunkGeneratorJob _ChunkGeneratorJob;
        private JobHandle _BuilderJobHandle;
        private JobHandle _GeneratorJobHandle;
        private int NonAirBlocksCount;

        public Mesh Mesh;
        public Block[] Blocks;
        public bool Built;
        public bool Building;
        public bool Generated;
        public bool Generating;
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
            _BuildTimer = new Stopwatch();
            _MeshTimer = new Stopwatch();

            Blocks = new Block[Size.x * Size.y * Size.z];
            Position = transform.position.ToInt();
            Built = Building = Generated = Generating = PendingMeshAssigment = false;
            PendingMeshUpdate = true;
            NonAirBlocksCount = 0;

            double waitTime = TimeSpan
                .FromTicks((DateTime.Now.Ticks - WorldController.InitialTick) % WorldController.WorldTickRate.Ticks)
                .TotalSeconds;
            InvokeRepeating(nameof(Tick), (float) waitTime, (float) WorldController.WorldTickRate.TotalSeconds);
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

        private (ChunkBuilderJob, JobHandle) Build()
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
                return default;
            }

            if (noiseMap == null)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to generate chunk at position ({Position.x}, {Position.z}): failed to get noise map.");
                Building = false;
                _BuildTimer.Stop();
                return default;
            }

            ChunkBuilderJob chunkBuilderJob = new ChunkBuilderJob(Blocks, noiseMap);
            return (chunkBuilderJob, chunkBuilderJob.Schedule(chunkBuilderJob.Blocks.Length, 250));
        }

        private (ChunkGeneratorJob, JobHandle) Generate()
        {
            if (!Built)
            {
                return default;
            }

            _MeshTimer.Restart();

            Generated = PendingMeshAssigment = false;
            Generating = true;


            ChunkGeneratorJob chunkGeneratorJob = new ChunkGeneratorJob(Position, Blocks);
            return (chunkGeneratorJob, chunkGeneratorJob.Schedule(Blocks.Length, 250));
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
                    _BuilderJobHandle.Complete();

                    Building = false;
                    Built = true;

                    NonAirBlocksCount = _ChunkBuilderJob.NonAirBlocksCount;
                    _ChunkBuilderJob.Blocks.CopyTo(Blocks);
                    _ChunkBuilderJob.Blocks.Dispose();

                    _BuildTimer.Stop();
                    DiagnosticsPanelController.ChunkBuildTimes.Enqueue(_BuildTimer.Elapsed.TotalMilliseconds);
                }
                else
                {
                    (_ChunkBuilderJob, _BuilderJobHandle) = Build();
                }
            }

            if (ChunkController.Current.AllChunksBuilt && PendingMeshUpdate)
            {
                if (Generating)
                {
                    if (_GeneratorJobHandle.IsCompleted)
                    {
                        _GeneratorJobHandle.Complete();

                        Generating = PendingMeshUpdate = false;
                        Generated = PendingMeshAssigment = true;

                        Mesh = _ChunkGeneratorJob.GetMesh();

                        _MeshTimer.Stop();
                        DiagnosticsPanelController.ChunkMeshTimes.Enqueue(_MeshTimer.Elapsed.TotalMilliseconds);
                    }
                }
                else
                {
                   (_ChunkGeneratorJob, _GeneratorJobHandle) = Generate();
                }
            }

            if (ExpensiveMeshing && PendingMeshAssigment && Generated)
            {
                AssignMesh();
            }
        }

        private void AssignMesh()
        {
            if (!Built || !Generated)
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