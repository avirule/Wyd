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
    public class WorldController : MonoBehaviour, IEntityChunkChangedSubscriber
    {
        public static WorldController Current;

        private GameObject _CollisionToken;
        private List<CollisionToken> _CollisionTokens;
        private Mesh _ColliderMesh;
        private bool _UpdateColliderMesh;

        public MeshCollider MeshCollider;
        public long InitialTick;
        public TimeSpan WorldTickRate;
        public float TicksPerSecond;
        public WorldGenerationSettings WorldGenerationSettings;
        public ChunkController ChunkController;
        public DateTime UpdateTime;

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

            _CollisionToken = Resources.Load<GameObject>($@"Prefabs/{nameof(CollisionToken)}");

            WorldTickRate = TimeSpan.FromSeconds(1d / TicksPerSecond);

            _CollisionTokens = new List<CollisionToken>();
            _ColliderMesh = new Mesh();
            _UpdateColliderMesh = false;

            InitialTick = DateTime.Now.Ticks;
        }

        private void Start()
        {
            PlayerController.Current.RegisterEntityChangedSubscriber(this);
        }

        private void Update()
        {
            UpdateTime = DateTime.Now;

            if (EntityChangedChunk)
            {
                UpdateChunkLoadArea(PlayerController.Current.CurrentChunk);

                EntityChangedChunk = false;
            }

            if (_UpdateColliderMesh)
            {
                GenerateColliderMesh();
            }
        }

        private void OnApplicationQuit()
        {
            Destroy(_ColliderMesh);
        }

        private void GenerateColliderMesh()
        {
            List<CombineInstance> combines = new List<CombineInstance>();

            foreach (CollisionToken collisionToken in _CollisionTokens)
            {
                if ((collisionToken.Mesh == default) || (collisionToken.Mesh.vertexCount == 0))
                {
                    continue;
                }

                CombineInstance combine = new CombineInstance
                {
                    mesh = collisionToken.Mesh,
                    transform = collisionToken.transform.localToWorldMatrix
                };

                combines.Add(combine);
            }

            _ColliderMesh.CombineMeshes(combines.ToArray(), true, true);
            _ColliderMesh.RecalculateNormals();
            _ColliderMesh.RecalculateTangents();
            _ColliderMesh.Optimize();

            MeshCollider.sharedMesh = _ColliderMesh;

            _UpdateColliderMesh = false;
        }

        public ushort GetBlockAt(Vector3 position)
        {
            return ChunkController.GetBlockAt(position);
        }

        public bool BlockExistsAt(Vector3 position)
        {
            return ChunkController.BlockExistsAt(position);
        }
        
        public bool IsOnBorrowedUpdateTime()
        {
            return (DateTime.Now - UpdateTime) > OptionsController.Current.MaximumInternalFrameTime;
        }
        
        #region ENTITY MANAGMENT

        public void RegisterEntity(Transform parent, int radius = 2)
        {
            GameObject entityToken = Instantiate(_CollisionToken, transform);
            CollisionToken collisionToken = entityToken.GetComponent<CollisionToken>();
            collisionToken.AuthorTransform = parent;
            collisionToken.Radius = radius;
            collisionToken.UpdatedMesh += OnEntityChangedMesh;

            _CollisionTokens.Add(collisionToken);
        }

        private void OnEntityChangedMesh(object sender, Mesh mesh)
        {
            _UpdateColliderMesh = true;
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