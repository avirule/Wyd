#region

using System.Collections;
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
        private BlockController _BlockController;
        private WorldController _WorldController;

        public Block[][][] Blocks;
        public Material BlocksMaterial;
        public bool Destroyed;
        public bool Generated;
        public bool Generating;
        public Mesh Mesh;
        public MeshCollider MeshCollider;
        public bool Meshed;
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

            yield return new WaitUntil(() => meshGenerator.Update() || Destroyed);

            if (Destroyed)
            {
                meshGenerator.Abort();
                Meshing = false;

                yield break;
            }

            Mesh = meshGenerator.GetMesh(ref Mesh);
            MeshCollider.sharedMesh = Mesh;

            Meshing = PendingUpdate = false;
            Meshed = true;
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
            Mesh = null;
            MeshCollider.sharedMesh = null;
            gameObject.SetActive(false);
        }
    }
}