using System;
using System.Collections;
using Controllers;
using Environment.Terrain.Generation;
using Logging;
using NLog;
using UnityEngine;

namespace Environment.Terrain
{
    public class Chunk : MonoBehaviour
    {
        public static Vector3Int Size = new Vector3Int(8, 32, 8);
        private BlockController _BlockController;
        private WorldController _WorldController;

        public string[][][] Blocks;
        public bool Generated;
        public bool Generating;
        public bool Meshed;
        public bool Meshing;
        public MeshFilter MeshFilter;
        public Vector3Int Position;

        private void Awake()
        {
            Generated = Meshed = false;

            _WorldController = GameObject.FindWithTag("WorldController").GetComponent<WorldController>();
            _BlockController = GameObject.FindWithTag("GameController").GetComponent<BlockController>();
        }

        public IEnumerator GenerateBlocks()
        {
            Generated = false;
            Generating = true;

            yield return new WaitUntil(() => _WorldController.NoiseMap.Ready);

            float[][] noiseMap =
                _WorldController.GetNoiseHeightsByPosition(Position, new Vector3Int(Size.x, Size.y, Size.z));

            if (noiseMap == null)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to generate chunk at position ({Position.x}, {Position.z}): failed to get noise map.");
                yield break;
            }

            ChunkGenerator chunkGenerator = new ChunkGenerator(noiseMap, Size);
            chunkGenerator.Start();
            yield return new WaitUntil(() => chunkGenerator.Update());

            Blocks = chunkGenerator.Blocks;

            Generating = false;
            Generated = true;
        }

        public IEnumerator GenerateMesh()
        {
            if (!Generated)
            {
                yield break;
            }

            Meshed = false;
            Meshing = true;
            
            MeshBuilder builder = new MeshBuilder(_WorldController, _BlockController, Position, Blocks);
            builder.Start();

            yield return new WaitUntil(() => builder.Update());

            MeshFilter.mesh = builder.GetMesh(MeshFilter.mesh);

            Meshing = false;
            Meshed = true;
        }
    }
}