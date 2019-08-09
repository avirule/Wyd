using System.Collections;
using Controllers;
using Controllers.Game;
using Environment.Terrain.Generation;
using Logging;
using NLog;
using UnityEngine;

namespace Environment.Terrain
{
    public class Chunk
    {
        public static Vector3Int Size = new Vector3Int(8, 32, 8);
        private readonly BlockController _BlockController;
        private readonly WorldController _WorldController;

        public string[][][] Blocks;
        public bool Destroy;
        public bool Generated;
        public bool Generating;
        public Mesh Mesh;
        public bool Meshed;
        public bool Meshing;
        public Vector3Int Position;

        public Chunk(WorldController worldController, BlockController blockController, Vector3Int position)
        {
            Generated = Generating = Meshed = Meshing = Destroy = false;

            _WorldController = worldController;
            _BlockController = blockController;

            Position = position;
        }

        public IEnumerator GenerateBlocks()
        {
            Generated = false;
            Generating = true;

            yield return new WaitUntil(() => _WorldController.NoiseMap.Ready);

            float[][] noiseMap = _WorldController.NoiseMap.GetSection(Position, Size);

            if (noiseMap == null)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    $"Failed to generate chunk at position ({Position.x}, {Position.z}): failed to get noise map.");
                yield break;
            }

            ChunkGenerator chunkGenerator = new ChunkGenerator(noiseMap, Size);
            chunkGenerator.Start();
            yield return new WaitUntil(() => chunkGenerator.Update() || Destroy);

            if (Destroy)
            {
                chunkGenerator.Abort();
                Generating = false;
                yield break;
            }

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

            MeshGenerator meshGenerator = new MeshGenerator(_WorldController, _BlockController, Position, Blocks);
            meshGenerator.Start();

            yield return new WaitUntil(() => meshGenerator.Update() || Destroy);

            if (Destroy)
            {
                meshGenerator.Abort();
                Meshing = false;
                yield break;
            }

            Mesh = meshGenerator.GetMesh(ref Mesh);

            Meshing = false;
            Meshed = true;
        }
    }
}