#region

using System;
using System.IO;
using System.Threading;
using NLog;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using Wyd.Controllers.World;
using Wyd.Game;
using Wyd.Game.World.Blocks;
using Wyd.System.Jobs;
using Wyd.System.Logging;
using Wyd.System.Logging.Targets;
using Object = UnityEngine.Object;

#endregion

namespace Wyd.Controllers.State
{
    public class GameController : SingletonController<GameController>
    {
        public static readonly int MainThreadId = Thread.CurrentThread.ManagedThreadId;

        private JobQueue JobExecutionQueue { get; set; }
        
        public int JobCount => JobExecutionQueue.JobCount;
        public int ActiveJobCount => JobExecutionQueue.ActiveJobCount;
        public int WorkerThreadCount => JobExecutionQueue.WorkerThreadCount;

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
            JobExecutionQueue = new JobQueue(TimeSpan.FromMilliseconds(200), OptionsController.Current.ThreadingMode,
                OptionsController.Current.CPUCoreUtilization);

            JobExecutionQueue.WorkerCountChanged += (sender, count) =>
                WorkerThreadCountChanged?.Invoke(sender, count);

            JobExecutionQueue.JobQueued += (sender, args) =>
                JobCountChanged?.Invoke(sender, JobExecutionQueue.JobCount);

            JobExecutionQueue.JobStarted += (sender, args) =>
            {
                JobCountChanged?.Invoke(sender, JobExecutionQueue.JobCount);
                ActiveJobCountChanged?.Invoke(sender, JobExecutionQueue.ActiveJobCount);
            };

            JobExecutionQueue.JobFinished += (sender, args) =>
            {
                JobFinished?.Invoke(sender, args);
                JobCountChanged?.Invoke(sender, JobExecutionQueue.JobCount);
                ActiveJobCountChanged?.Invoke(sender, JobExecutionQueue.ActiveJobCount);
            };

            OptionsController.Current.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName.Equals(nameof(OptionsController.Current.ThreadingMode)))
                {
                    JobExecutionQueue.ThreadingMode = OptionsController.Current.ThreadingMode;
                }
                else if (args.PropertyName.Equals(nameof(OptionsController.Current.CPUCoreUtilization)))
                {
                    JobExecutionQueue.ModifyWorkerThreadCount(OptionsController.Current.CPUCoreUtilization);
                }
            };

            JobExecutionQueue.Start();

            RegisterDefaultBlocks();
        }

#if UNITY_EDITOR
        private void LateUpdate()
        {
            UnityDebuggerTarget.Flush();
        }
#endif

        private void OnApplicationQuit()
        {
            // Deallocate and destroy ALL NativeCollection / disposable objects
            JobExecutionQueue.Abort();
            LogManager.Shutdown();
        }

        private void RegisterDefaultBlocks()
        {
            BlockController.Current.RegisterBlockRules("bedrock", Block.Types.None, false, true, true, false);
            BlockController.Current.RegisterBlockRules("grass", Block.Types.Raw, false, true, true, true,
                (position, direction) =>
                {
                    Vector3 positionAbove = position + Vector3.up;
                    WorldController.Current.TryGetBlockAt(positionAbove, out Block block);

                    if (!block.Transparent)
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
                });
            BlockController.Current.RegisterBlockRules("dirt", Block.Types.Raw, false, true, true, true);
            BlockController.Current.RegisterBlockRules("stone", Block.Types.None, false, true, true, true);
            BlockController.Current.RegisterBlockRules("glass", Block.Types.Raw, true, true, true, true);
            BlockController.Current.RegisterBlockRules("coal_ore", Block.Types.Ore, false, true, true, true);
            BlockController.Current.RegisterBlockRules("gold_ore", Block.Types.Ore, false, true, true, true);
            BlockController.Current.RegisterBlockRules("diamond_ore", Block.Types.Ore, false, true, true, true);
            BlockController.Current.RegisterBlockRules("oak_log", Block.Types.None, false, true, true, true,
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
                });
            BlockController.Current.RegisterBlockRules("oak_leaf", Block.Types.None, false, true, true, true);
            BlockController.Current.RegisterBlockRules("oak_leaf_apple", Block.Types.None, false, true, true, true);
            BlockController.Current.RegisterBlockRules("sand", Block.Types.Raw, false, true, true, true);
            BlockController.Current.RegisterBlockRules("water", Block.Types.Raw, true, false, false, false);
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

        public bool TryQueueJob(Job job, out object identity) => JobExecutionQueue.TryQueueJob(job, out identity);
    }
}
