using System.Collections;
using Controllers;
using Environment.Terrain.Generation;
using Logging;
using NLog;
using Static;
using UnityEngine;

namespace Environment.Terrain
{
    public class Chunk : MonoBehaviour
    {
        private WorldController _WorldController;
        private BlockController _BlockController;

        public static Vector3Int Size = new Vector3Int(8, 32, 8);
        
        public string[][][] Blocks;
        public bool Generated;
        public MeshCollider MeshCollider;
        public bool Meshed;
        public MeshFilter MeshFilter;
        public Vector3Int Position;

        private void Awake()
        {
            Generated = Meshed = false;

            _WorldController = GameObject.FindWithTag("WorldController").GetComponent<WorldController>();
            _BlockController = GameObject.FindWithTag("GameController").GetComponent<BlockController>();
            
            transform.parent = _WorldController.transform;

            Position = transform.position.ToInt();
        }

        public IEnumerator Generate(bool generateBlocks)
        {
            if (generateBlocks)
            {
                yield return new WaitUntil(() => _WorldController.NoiseMap.Ready);

                float[][] noiseMap = _WorldController.GetNoiseHeightsByPosition(Position, new Vector3Int(Size.x, Size.y, Size.z));

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
                Generated = true;
            }

            if (!Generated)
            {
                yield break;
            }

            MeshBuilder builder = new MeshBuilder(_WorldController, _BlockController, Position, Blocks);
            builder.Start();

            yield return new WaitUntil(() => builder.Update());

            if (MeshFilter == null)
            {
                yield break;
            }

            MeshFilter.mesh = builder.GetMesh(MeshFilter.mesh);
            MeshCollider.sharedMesh = MeshFilter.sharedMesh;

            Meshed = true;
        }
    }
}