#region

using System;
using System.Collections.Generic;
using Controllers.Entity;
using Controllers.Game;
using Game.Entity;
using Game.World;
using Logging;
using NLog;
using UnityEngine;
using UnityEngine.SceneManagement;

#endregion

namespace Controllers.World
{
    public class WorldController : MonoBehaviour, IEntityChunkChangedSubscriber
    {
        public static WorldController Current;

        private GameObject _EntityToken;
        private List<GameObject> _EntityTokens;

        public long InitialTick;
        public TimeSpan WorldTickRate;
        public float TicksPerSecond;
        public WorldGenerationSettings WorldGenerationSettings;
        public ChunkController ChunkController;

        public bool EntityChangedChunk { get; set; }

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

            _EntityToken = Resources.Load<GameObject>(@"Prefabs\EntityToken");

            WorldTickRate = TimeSpan.FromSeconds(1d / TicksPerSecond);

            _EntityTokens = new List<GameObject>();

            InitialTick = DateTime.Now.Ticks;
        }

        private void Start()
        {
            PlayerController.Current.RegisterEntityChangedSubscriber(this);
        }

        private void Update()
        {
            if (EntityChangedChunk)
            {
                UpdateChunkLoadArea(PlayerController.Current.CurrentChunk);

                EntityChangedChunk = false;
            }
        }

        public static Vector3 GetWorldChunkOriginFromGlobalPosition(Vector3 globalPosition)
        {
            return globalPosition.Divide(Chunk.Size).Floor().Multiply(Chunk.Size);
        }

        public ushort GetBlockAtPosition(Vector3 position)
        {
            return ChunkController.GetIdAtPosition(position);
        }


        #region ENTITY MANAGMENT

        public void RegisterEntity(Transform parent)
        {
            GameObject entityToken = Instantiate(_EntityToken, transform);
            entityToken.GetComponent<EntityTransformToken>().ParentEntityTransform = parent;

            _EntityTokens.Add(entityToken);
        }

        #endregion


        #region ON EVENT

        private void UpdateChunkLoadArea(Vector3Int chunkPosition)
        {
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