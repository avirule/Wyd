#region

using System;
using System.Collections.Generic;
using Controllers.Entity;
using Controllers.Game;
using Game;
using Game.Entity;
using Game.World;
using Game.World.Chunk;
using Logging;
using NLog;
using UnityEngine;
using UnityEngine.SceneManagement;

#endregion

namespace Controllers.World
{
    public class WorldController : SingletonController<WorldController>
    {
        public CollisionTokenController CollisionTokenController;
        public WorldGenerationSettings WorldGenerationSettings;
        public ChunkController ChunkController;
        public float TicksPerSecond;

        public long InitialTick { get; private set; }
        public TimeSpan WorldTickRate { get; private set; }
        public bool PrimaryLoaderChangedChunk { get; set; }
        public DateTime UpdateTime { get; private set; }

        private void Awake()
        {
            if (GameController.Current == default)
            {
                SceneManager.LoadSceneAsync("Scenes/MainMenu", LoadSceneMode.Single);
            }

            AssignCurrent(this);

            if (TicksPerSecond < 1)
            {
                EventLog.Logger.Log(LogLevel.Error,
                    "World tick rate cannot be set to less than 1tick/s. Exiting game.");
                GameController.ApplicationClose();
                return;
            }

            WorldTickRate = TimeSpan.FromSeconds(1d / TicksPerSecond);

            InitialTick = DateTime.Now.Ticks;
        }

        private void Update()
        {
            UpdateTime = DateTime.Now;

            if (PrimaryLoaderChangedChunk)
            {
                UpdateChunkLoadArea(PlayerController.Current.CurrentChunk);

                PrimaryLoaderChangedChunk = false;
            }
        }

        public bool IsOnBorrowedUpdateTime()
        {
            return (DateTime.Now - UpdateTime) > OptionsController.Current.MaximumInternalFrameTime;
        }

        public void RegisterEntity(Transform attachTo, int loadRadius)
        {
            CollisionTokenController.RegisterEntity(attachTo, loadRadius);   
        }

        #region ON EVENT

        private void UpdateChunkLoadArea(Vector3Int chunkPosition)
        {
            EnqueueBuildChunkArea(chunkPosition,
                WorldGenerationSettings.Radius + OptionsController.Current.PreLoadChunkDistance);
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