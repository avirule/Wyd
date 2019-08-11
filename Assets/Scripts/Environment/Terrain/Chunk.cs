#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Controllers.Game;
using Controllers.World;
using Environment.Terrain.Generation;
using Logging;
using NLog;
using Static;
using UnityEngine;

#endregion

namespace Environment.Terrain
{
    public class Chunk : MonoBehaviour
    {
        public static Vector3Int Size = new Vector3Int(8, 32, 8);
        public static readonly List<float> ChunkBuildTimes = new List<float>();
        public static readonly List<float> ChunkMeshTimes = new List<float>();

        private BlockController _BlockController;
        private WorldController _WorldController;
        private ChunkController _ChunkController;

        public Block[] Blocks;
        public bool Destroyed;
        public bool Generated;
        public bool Generating;
        public bool Meshed;
        public bool Meshing;
        public bool PendingMeshUpdate;
        public MeshFilter MeshFilter;
        public MeshRenderer MeshRenderer;
        public MeshCollider MeshCollider;
        public Vector3Int Position;


        private void Awake()
        {
            GameObject worldControllerObject = GameObject.FindWithTag("WorldController");

            transform.parent = worldControllerObject.transform;
            _WorldController = worldControllerObject.GetComponent<WorldController>();
            _ChunkController = worldControllerObject.GetComponent<ChunkController>();
            _BlockController = GameObject.FindWithTag("GameController").GetComponent<BlockController>();
        }

        public IEnumerator GenerateBlocks()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            Generated = false;
            Generating = true;

            yield return new WaitUntil(() => _WorldController.NoiseMap.Ready || Destroyed);

            if (Destroyed)
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

            yield return new WaitUntil(() => chunkGenerator.Update() || Destroyed);

            if (Destroyed || !Generating)
            {
                chunkGenerator.Abort();
                Generating = false;
                yield break;
            }

            Blocks = chunkGenerator.Blocks;

            Generating = false;
            Generated = true;

            stopwatch.Stop();
            ChunkBuildTimes.Add(stopwatch.ElapsedMilliseconds);

            if (ChunkMeshTimes.Count > _ChunkController.MaximumChunkLoadTimeCaching)
            {
                ChunkMeshTimes.RemoveRange(0, ChunkBuildTimes.Count - _ChunkController.MaximumChunkLoadTimeCaching);
            }
        }

        public IEnumerator GenerateMesh()
        {
            if (!Generated)
            {
                yield break;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();

            Meshed = false;
            Meshing = true;

            MeshGenerator meshGenerator = new MeshGenerator(_WorldController, _BlockController, Position, Blocks);
            meshGenerator.Start();

            yield return new WaitUntil(() => meshGenerator.Update() || Destroyed);

            if (Destroyed)
            {
                meshGenerator.Abort();
                Meshing = false;

                yield break;
            }

            MeshFilter.mesh = meshGenerator.GetMesh();
            MeshCollider.sharedMesh = MeshFilter.sharedMesh;

            Meshing = PendingMeshUpdate = false;
            Meshed = true;

            stopwatch.Stop();

            ChunkMeshTimes.Add(stopwatch.ElapsedMilliseconds);

            if (ChunkMeshTimes.Count > _ChunkController.MaximumChunkLoadTimeCaching)
            {
                ChunkMeshTimes.RemoveRange(0, ChunkMeshTimes.Count - _ChunkController.MaximumChunkLoadTimeCaching);
            }
        }

        public void Initialise(Vector3 position = default)
        {
            transform.position = position;
            Position = position.ToInt();
            PendingMeshUpdate = true;
            Generated = Generating = Meshed = Meshing = Destroyed = false;
            gameObject.SetActive(true);
        }

        public void Destroy()
        {
            Destroyed = true;
            transform.position = new Vector3(0f, 0f, 0f);
            Position = new Vector3Int(0, 0, 0);
            MeshFilter.mesh.Clear();
            MeshCollider.sharedMesh.Clear();
            gameObject.SetActive(false);
        }

        public void Tick()
        {
        }
    }
}