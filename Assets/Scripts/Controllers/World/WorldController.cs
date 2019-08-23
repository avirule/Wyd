#region

using System;
using Controllers.Entity;
using Controllers.Game;
using Environment.Terrain;
using Environment.Terrain.Generation;
using Logging;
using NLog;
using Noise;
using Static;
using UnityEngine;
using UnityEngine.SceneManagement;

#endregion

namespace Controllers.World
{
    public class WorldController : MonoBehaviour
    {
        public static WorldController Current;

        public long InitialTick;

        /// <summary>
        ///     This is referenced OFTEN in SYNCHRONOUS CONTEXT. DO NOT USE IN ASYNCHRONOUS CONTEXTS.
        /// </summary>
        public TimeSpan WorldTickRate;

        public float TicksPerSecond;
        public WorldGenerationSettings WorldGenerationSettings;
        public ChunkController ChunkController;
        public NoiseMap NoiseMap;

        private void Awake()
        {
            if (GameController.Current == default)
            {
                SceneManager.LoadSceneAsync("Scenes/MainMenu", LoadSceneMode.Single);
            }

            if ((Current != null) && (Current != this))
            {
                Destroy(gameObject);
            }
            else
            {
                Current = this;
            }

            if (TicksPerSecond < 1)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    "World tick rate cannot be set to less than 1tick/s. Exiting game.");
                GameController.ApplicationClose();
                return;
            }

            WorldTickRate = TimeSpan.FromSeconds(1d / TicksPerSecond);

            NoiseMap = new NoiseMap(null, Vector3Int.zero,
                new Vector3Int((WorldGenerationSettings.Diameter + 1) * Chunk.Size.x, 0,
                    (WorldGenerationSettings.Diameter + 1) * Chunk.Size.z));

            InitialTick = DateTime.Now.Ticks;
        }

        private void Start()
        {
            PlayerController.Current.ChunkChanged += UpdateChunkLoadArea;
        }

        public static Vector3 GetWorldChunkOriginFromGlobalPosition(Vector3 globalPosition)
        {
            return globalPosition.Divide(Chunk.Size).Floor().Multiply(Chunk.Size);
        }

        public Block GetBlockAtPosition(Vector3Int position)
        {
            return ChunkController.GetBlockAtPosition(position);
        }


        #region ON UPDATE()

        private void UpdateChunkLoadArea(object sender, Vector3Int chunkPosition)
        {
            NoiseMap.Generate(chunkPosition, NoiseMap.Bounds.size, WorldGenerationSettings);
            EnqueueBuildChunkArea(chunkPosition, WorldGenerationSettings.Radius);
        }

        public void EnqueueBuildChunkArea(Vector3Int origin, int radius)
        {
            // +1 to include player's chunk
            for (int x = -radius; x < (radius + 1); x++)
            {
                for (int z = -radius; z < (radius + 1); z++)
                {
                    Vector3Int position = origin + new Vector3Int(x, 0, z).Multiply(Chunk.Size);

                    ChunkController.BuildChunkQueue.Enqueue(position);
                }
            }
        }

        #endregion
    }
}