#region

using System;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wyd.Controllers.World;
using Wyd.Game;
using Wyd.Game.World.Blocks;
using Wyd.System.Jobs;
using Object = UnityEngine.Object;

#endregion

namespace Wyd.Controllers.State
{
    public class GameController : SingletonController<GameController>
    {
        public static readonly int MainThreadId = Thread.CurrentThread.ManagedThreadId;

        private JobScheduler _JobExecutionScheduler;

        public int JobCount => _JobExecutionScheduler.JobCount;
        public int ActiveJobCount => _JobExecutionScheduler.ProcessingJobCount;
        public int WorkerThreadCount => _JobExecutionScheduler.WorkerThreadCount;

        public event JobFinishedEventHandler JobFinished;
        public event EventHandler<int> JobCountChanged;
        public event EventHandler<int> ActiveJobCountChanged;
        public event EventHandler<int> WorkerThreadCountChanged;

        private void Awake()
        {
            AssignCurrent(this);
            DontDestroyOnLoad(this);
            QualitySettings.vSyncCount = 0;
        }

        private void Start()
        {
            _JobExecutionScheduler = new JobScheduler(TimeSpan.FromMilliseconds(200), OptionsController.Current.ThreadingMode,
                OptionsController.Current.CPUCoreUtilization);

            _JobExecutionScheduler.WorkerCountChanged += (sender, count) =>
                WorkerThreadCountChanged?.Invoke(sender, count);

            _JobExecutionScheduler.JobQueued += (sender, args) =>
                JobCountChanged?.Invoke(sender, _JobExecutionScheduler.JobCount);

            _JobExecutionScheduler.JobStarted += (sender, args) =>
            {
                JobCountChanged?.Invoke(sender, _JobExecutionScheduler.JobCount);
                ActiveJobCountChanged?.Invoke(sender, _JobExecutionScheduler.ProcessingJobCount);
            };

            _JobExecutionScheduler.JobFinished += (sender, args) =>
            {
                JobFinished?.Invoke(sender, args);
                JobCountChanged?.Invoke(sender, _JobExecutionScheduler.JobCount);
                ActiveJobCountChanged?.Invoke(sender, _JobExecutionScheduler.ProcessingJobCount);
            };

            OptionsController.Current.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName.Equals(nameof(OptionsController.Current.ThreadingMode)))
                {
                    _JobExecutionScheduler.ThreadingMode = OptionsController.Current.ThreadingMode;
                }
                else if (args.PropertyName.Equals(nameof(OptionsController.Current.CPUCoreUtilization)))
                {
                    _JobExecutionScheduler.ModifyWorkerThreadCount(OptionsController.Current.CPUCoreUtilization);
                }
            };

            _JobExecutionScheduler.Start();

            RegisterDefaultBlocks();
        }

        private void OnDestroy()
        {
            // Deallocate and destroy ALL NativeCollection / disposable objects
            _JobExecutionScheduler.Abort();
        }

        private void RegisterDefaultBlocks()
        {
            BlockController.Current.RegisterBlockRule("bedrock", Block.Types.None, null,
                BlockRule.Property.Collideable);

            BlockController.Current.RegisterBlockRule("grass", Block.Types.Raw,
                (position, direction) =>
                {
                    Vector3 positionAbove = position + Vector3.up;
                    WorldController.Current.TryGetBlockAt(positionAbove, out ushort blockId);

                    if (!BlockController.Current.CheckBlockHasProperty(blockId, BlockRule.Property.Transparent))
                    {
                        return "dirt";
                    }

                    switch (direction)
                    {
                        // todo decide on whether to use this ??
//                    case Direction.North:
//                        string northCheck = worldController.GetBlockAtPosition(position + new Vector3Int(0, -1, 1));
//
//                        return northCheck.Equals("Grass") ? "Grass" : "Grass_Side";
//                    case Direction.East:
//                        string eastCheck = worldController.GetBlockAtPosition(position + new Vector3Int(1, -1, 0));
//
//                        return eastCheck.Equals("Grass") ? "Grass" : "Grass_Side";
//                    case Direction.South:
//                        string southCheck = worldController.GetBlockAtPosition(position + new Vector3Int(0, -1, -1));
//
//                        return southCheck.Equals("Grass") ? "Grass" : "Grass_Side";
//                    case Direction.West:
//                        string westCheck = worldController.GetBlockAtPosition(position + new Vector3Int(-1, -1, 0));
//
//                        return westCheck.Equals("Grass") ? "Grass" : "Grass_Side";

                        case Direction.North:
                        case Direction.East:
                        case Direction.South:
                        case Direction.West:
                            return "grass_side";
                        case Direction.Up:
                            return "grass";
                        case Direction.Down:
                            return "dirt";
                        default:
                            throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
                    }
                }, BlockRule.Property.Collectible, BlockRule.Property.Collideable,
                BlockRule.Property.Destroyable);

            BlockController.Current.RegisterBlockRule("dirt", Block.Types.Raw, null,
                BlockRule.Property.Collectible, BlockRule.Property.Collideable,
                BlockRule.Property.Destroyable);

            BlockController.Current.RegisterBlockRule("stone", Block.Types.None, null,
                BlockRule.Property.Collectible, BlockRule.Property.Collideable,
                BlockRule.Property.Destroyable);

            BlockController.Current.RegisterBlockRule("glass", Block.Types.Raw, null,
                BlockRule.Property.Transparent, BlockRule.Property.Collectible,
                BlockRule.Property.Collideable, BlockRule.Property.Destroyable);

            BlockController.Current.RegisterBlockRule("coal_ore", Block.Types.Ore, null,
                BlockRule.Property.Collectible, BlockRule.Property.Collideable,
                BlockRule.Property.Destroyable);

            BlockController.Current.RegisterBlockRule("gold_ore", Block.Types.Ore, null,
                BlockRule.Property.Collectible, BlockRule.Property.Collideable,
                BlockRule.Property.Destroyable);

            BlockController.Current.RegisterBlockRule("diamond_ore", Block.Types.Ore, null,
                BlockRule.Property.Collectible, BlockRule.Property.Collideable,
                BlockRule.Property.Destroyable);

            BlockController.Current.RegisterBlockRule("oak_log", Block.Types.None,
                (vector3, direction) =>
                {
                    switch (direction)
                    {
                        case Direction.North:
                        case Direction.East:
                        case Direction.South:
                        case Direction.West:
                            return "oak_log";
                        case Direction.Up:
                        case Direction.Down:
                            return "oak_log_inner";
                        default:
                            throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
                    }
                },
                BlockRule.Property.Collectible, BlockRule.Property.Collideable,
                BlockRule.Property.Destroyable);

            BlockController.Current.RegisterBlockRule("oak_leaf", Block.Types.None, null,
                BlockRule.Property.Collectible, BlockRule.Property.Collideable,
                BlockRule.Property.Destroyable);

            BlockController.Current.RegisterBlockRule("oak_leaf_apple", Block.Types.None, null,
                BlockRule.Property.Collectible, BlockRule.Property.Collideable,
                BlockRule.Property.Destroyable);

            BlockController.Current.RegisterBlockRule("sand", Block.Types.Raw, null,
                BlockRule.Property.Collectible, BlockRule.Property.Collideable,
                BlockRule.Property.Destroyable);

            BlockController.Current.RegisterBlockRule("water", Block.Types.Raw, null,
                BlockRule.Property.Transparent);
        }

        public static T LoadResource<T>(string path) where T : Object
        {
            T resource = Resources.Load<T>(path);
            Resources.UnloadUnusedAssets();
            return resource;
        }

        public static T[] LoadAllResources<T>(string path) where T : Object
        {
            T[] resources = Resources.LoadAll<T>(path);
            Resources.UnloadUnusedAssets();
            return resources;
        }

        public static void QuitToMainMenu()
        {
            SceneManager.LoadSceneAsync("Scenes/MainMenu", LoadSceneMode.Single);
        }

        public static void ApplicationClose(int exitCode = -1)
        {
            Application.Quit(exitCode);

#if UNITY_EDITOR
            EditorApplication.ExitPlaymode();
#endif
        }

        public bool TryQueueJob(Job job, out object identity) => _JobExecutionScheduler.TryQueueJob(job, out identity);
    }
}
