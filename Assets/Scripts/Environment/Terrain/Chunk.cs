#region

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
        public const int MAXIMUM_CHUNK_LOAD_TIME_CACHING = 60;
        public static Vector3Int Size = new Vector3Int(8, 32, 8);
        public static readonly List<float> ChunkBuildTimes = new List<float>();
        public static readonly List<float> ChunkMeshTimes = new List<float>();

        private BlockController _BlockController;
        private WorldController _WorldController;

        public Block[] Blocks;
        public bool Destroyed;
        public bool Generated;
        public bool Generating;
        public MeshCollider MeshCollider;
        public bool Meshed;
        public MeshFilter MeshFilter;
        public bool Meshing;
        public bool PendingUpdate;
        public Vector3Int Position;


        private void Awake()
        {
            transform.parent = GameObject.FindWithTag("WorldController").transform;
            _WorldController = GameObject.FindWithTag("WorldController").GetComponent<WorldController>();
            _BlockController = GameObject.FindWithTag("GameController").GetComponent<BlockController>();
        }

        public IEnumerator GenerateBlocks()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            Generated = false;
            Generating = true;

            float[][] noiseMap = _WorldController.NoiseMap.GetSection(Position, Size);

            if (noiseMap == null)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to generate chunk at position ({Position.x}, {Position.z}): failed to get noise map.");
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

            if (ChunkMeshTimes.Count > MAXIMUM_CHUNK_LOAD_TIME_CACHING)
            {
                ChunkMeshTimes.RemoveRange(0, ChunkBuildTimes.Count - MAXIMUM_CHUNK_LOAD_TIME_CACHING);
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

            Meshing = PendingUpdate = false;
            Meshed = true;

            stopwatch.Stop();

            ChunkMeshTimes.Add(stopwatch.ElapsedMilliseconds);

            if (ChunkMeshTimes.Count > MAXIMUM_CHUNK_LOAD_TIME_CACHING)
            {
                ChunkMeshTimes.RemoveRange(0, ChunkMeshTimes.Count - MAXIMUM_CHUNK_LOAD_TIME_CACHING);
            }
        }

        public void Initialise(Vector3 position = default)
        {
            transform.position = position;
            Position = position.ToInt();
            PendingUpdate = true;
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