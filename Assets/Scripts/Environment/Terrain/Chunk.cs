#region

using System;
using System.Collections;
using System.Diagnostics;
using Controllers.Game;
using Controllers.UI;
using Controllers.World;
using Environment.Terrain.Generation;
using Logging;
using NLog;
using Static;
using Threading.Generation;
using UnityEngine;

#endregion

namespace Environment.Terrain
{
    public class Chunk : MonoBehaviour
    {
        public static Vector3Int Size = new Vector3Int(16, 16, 16);

        private BlockController _BlockController;
        private WorldController _WorldController;
        private Mesh _Mesh;

        public Block[] Blocks;
        public bool Deactivated;
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


        private void Awake()
        {
            GameObject worldControllerObject = GameObject.FindWithTag("WorldController");

            transform.parent = worldControllerObject.transform;
            _WorldController = worldControllerObject.GetComponent<WorldController>();
            _BlockController = GameObject.FindWithTag("GameController").GetComponent<BlockController>();
        }

        public IEnumerator GenerateBlocks()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            Generated = false;
            Generating = true;

            yield return new WaitUntil(() => _WorldController.NoiseMap.Ready || Deactivated);

            if (Deactivated)
            {
                Generating = false;
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
                yield break;
            }

            if (noiseMap == null)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to generate chunk at position ({Position.x}, {Position.z}): failed to get noise map.");
                Generating = false;
                yield break;
            }

            ChunkGenerator chunkGenerator = new ChunkGenerator(noiseMap, Size);
            chunkGenerator.Start();

            yield return new WaitUntil(() => chunkGenerator.Update() || Deactivated);

            if (Deactivated || !Generating)
            {
                chunkGenerator.Abort();
                Generating = false;
                yield break;
            }

            Blocks = chunkGenerator.Blocks;

            Generating = false;
            Generated = true;

            stopwatch.Stop();
            DiagnosticsController.ChunkBuildTimes.Enqueue(stopwatch.ElapsedMilliseconds);
        }

        public IEnumerator GenerateMesh()
        {
            if (!Generated)
            {
                yield break;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            Meshed = PendingMeshAssigment = false;
            Meshing = true;

            MeshGenerator meshGenerator = new MeshGenerator(_WorldController, _BlockController, Position, Blocks);
            meshGenerator.Start();

            yield return new WaitUntil(() => meshGenerator.Update() || Deactivated);

            if (Deactivated)
            {
                meshGenerator.Abort();
                Meshing = false;

                yield break;
            }

            _Mesh = meshGenerator.GetMesh();

            Meshing = PendingMeshUpdate = false;
            Meshed = PendingMeshAssigment = true;

            stopwatch.Stop();

            DiagnosticsController.ChunkMeshTimes.Enqueue(stopwatch.ElapsedMilliseconds);
        }

        public void Activate(Vector3 position = default)
        {
            transform.position = position;
            Position = position.ToInt();
            PendingMeshUpdate = true;
            Generated = Generating = Meshed = Meshing = Deactivated = false;
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            Deactivated = true;
            transform.position = new Vector3(0f, 0f, 0f);
            Position = new Vector3Int(0, 0, 0);
            _Mesh.Clear();
            MeshFilter.mesh.Clear();
            MeshCollider.sharedMesh.Clear();
            gameObject.SetActive(false);
        }

        public void AssignMesh()
        {
            if (!Meshed)
            {
                return;
            }

            MeshFilter.mesh = _Mesh;
            MeshCollider.sharedMesh = MeshFilter.sharedMesh;
        }

        public void Tick()
        {
        }
    }
}