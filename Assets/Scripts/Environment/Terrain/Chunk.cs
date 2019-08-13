#region

using System;
using System.Collections;
using System.Diagnostics;
using Controllers.Game;
using Controllers.UI;
using Controllers.World;
using Logging;
using NLog;
using Static;
using Threading.Generation;
using UnityEngine;
using UnityEngine.Rendering;

#endregion

namespace Environment.Terrain
{
    public class Chunk : MonoBehaviour
    {
        public static Vector3Int Size = new Vector3Int(16, 16, 16);

        private BlockController _BlockController;
        private WorldController _WorldController;
        private Stopwatch _BuildTimer;
        private Stopwatch _MeshTimer;

        public Mesh Mesh;
        public Block[] Blocks;
        public bool Generated;
        public bool Generating;
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
            _WorldController = GameObject.FindWithTag("WorldController").GetComponent<WorldController>();
            _BlockController = GameObject.FindWithTag("GameController").GetComponent<BlockController>();
            _BuildTimer = new Stopwatch();
            _MeshTimer = new Stopwatch();

            Position = transform.position.ToInt();
            Generated = Generating = Meshed = Meshing = PendingMeshAssigment = false;
            PendingMeshUpdate = true;
        }

        private void Update()
        {
            GenerationCheckAndStart();

            CheckSettingsAndSet();

            Tick();
        }

        private void LateUpdate()
        {
            if (!ExpensiveMeshing)
            {
                Graphics.DrawMesh(Mesh, transform.localToWorldMatrix, MeshRenderer.material, 0);
            }
        }

        private void GenerationCheckAndStart()
        {
            if (!Generated && !Generating)
            {
                StartCoroutine(GenerateBlocks());
            }
            else if (_WorldController.ChunkController.AllChunksGenerated && PendingMeshUpdate && !Meshing)
            {
                StartCoroutine(GenerateMesh());
            }

            if (ExpensiveMeshing && PendingMeshAssigment && Meshed)
            {
                AssignMesh();
            }
        }

        private IEnumerator GenerateBlocks()
        {
            _BuildTimer.Restart();

            Generated = false;
            Generating = true;

            yield return new WaitUntil(() => _WorldController.NoiseMap.Ready || !Active);

            if (!Active)
            {
                Generating = false;
                _BuildTimer.Stop();
                yield break;
            }

            float[][] noiseMap;

            try
            {
                // todo fix retrieval of noise values without errors when player is moving extremely fast

                noiseMap = _WorldController.NoiseMap.GetSection(Position, Size);
            }
            catch (Exception)
            {
                Generating = false;
                _BuildTimer.Stop();
                yield break;
            }

            if (noiseMap == null)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to generate chunk at position ({Position.x}, {Position.z}): failed to get noise map.");
                Generating = false;
                _BuildTimer.Stop();
                yield break;
            }

            ChunkBuilder chunkGenerator = new ChunkBuilder(noiseMap, Size);
            chunkGenerator.Start();

            yield return new WaitUntil(() => chunkGenerator.Update() || !Active);

            if (!Active)
            {
                chunkGenerator.Abort();
                Generating = false;
                _BuildTimer.Stop();
                yield break;
            }

            Blocks = chunkGenerator.Blocks;

            Generating = false;
            Generated = true;

            _BuildTimer.Stop();
            DiagnosticsController.ChunkBuildTimes.Enqueue(_BuildTimer.Elapsed.TotalMilliseconds);
        }

        private IEnumerator GenerateMesh()
        {
            if (!Generated)
            {
                yield break;
            }

            _MeshTimer.Restart();

            Meshed = PendingMeshAssigment = false;
            Meshing = true;

            MeshGenerator meshGenerator = new MeshGenerator(_WorldController, _BlockController, Position, Blocks);
            meshGenerator.Start();

            yield return new WaitUntil(() => meshGenerator.Update() || !Active);

            if (!Active)
            {
                meshGenerator.Abort();
                Meshing = false;
                _MeshTimer.Stop();
                yield break;
            }

            Mesh = meshGenerator.GetMesh();

            Meshing = PendingMeshUpdate = false;
            Meshed = PendingMeshAssigment = true;

            _MeshTimer.Stop();
            DiagnosticsController.ChunkMeshTimes.Enqueue(_MeshTimer.Elapsed.TotalMilliseconds);
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

        public void Tick()
        {
        }

        private void AssignMesh()
        {
            if (!Generated || !Meshed)
            {
                return;
            }

            MeshFilter.mesh = Mesh;
            MeshCollider.sharedMesh = MeshFilter.sharedMesh;
            PendingMeshAssigment = false;
        }

        private void CheckSettingsAndSet()
        {
            Vector3Int difference = (Position - _WorldController.ChunkLoaderCurrentChunk).Abs();

            DrawShadows = CheckDrawShadows(difference);
            ExpensiveMeshing = CheckExpensiveMeshing(difference);
        }

        private void UpdateDrawShadows()
        {
            MeshRenderer.receiveShadows = DrawShadows;
            MeshRenderer.shadowCastingMode = DrawShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        private void UpdateExpensiveMeshing()
        {
            MeshRenderer.enabled = ExpensiveMeshing;
            MeshCollider.enabled = ExpensiveMeshing;
        }

        private bool CheckDrawShadows(Vector3Int difference)
        {
            return (Position == _WorldController.ChunkLoaderCurrentChunk) ||
                   ((difference.x <= (GameController.SettingsController.ShadowRadius * Size.x)) &&
                    (difference.z <= (GameController.SettingsController.ShadowRadius * Size.z)));
        }

        private static bool CheckExpensiveMeshing(Vector3Int difference)
        {
            return (difference.x <= (GameController.SettingsController.ExpensiveMeshingRadius * Size.x)) &&
                   (difference.z <= (GameController.SettingsController.ExpensiveMeshingRadius * Size.z));
        }
    }
}