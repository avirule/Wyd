#region

using System;
using System.Threading;
using Controllers.World;
using Game;
using Game.World.Blocks;
using Jobs;
using Logging.Targets;
using NLog;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

#endregion

namespace Controllers.State
{
    public class GameController : SingletonController<GameController>
    {
        private static JobQueue JobExecutionQueue { get; set; }

        public static readonly int MainThreadId = Thread.CurrentThread.ManagedThreadId;

        public event EventHandler<JobFinishedEventArgs> JobFinished;

        private void Awake()
        {
            AssignCurrent(this);
            DontDestroyOnLoad(this);
            QualitySettings.vSyncCount = 0;
        }

        private void Start()
        {
            if (JobExecutionQueue == default)
            {
                // init ThreadedQueue with # of threads matching 1/2 of logical processors
                JobExecutionQueue = new JobQueue(200, () => OptionsController.Current.ThreadingMode);
                JobExecutionQueue.Start();
            }

            RegisterDefaultBlocks();

            JobExecutionQueue.JobFinished += (sender, args) => { JobFinished?.Invoke(sender, args); };
            JobExecutionQueue.ModifyThreadPoolSize(OptionsController.Current.CPUCoreUtilization);
            OptionsController.Current.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName.Equals(nameof(OptionsController.Current.CPUCoreUtilization)))
                {
                    JobExecutionQueue.ModifyThreadPoolSize(OptionsController.Current.CPUCoreUtilization);
                }
            };
        }

        private void LateUpdate()
        {
            UnityDebuggerTarget.Flush();
        }

        private void OnApplicationQuit()
        {
            // Deallocate and destroy ALL NativeCollection / disposable objects
            JobExecutionQueue.Abort();
            LogManager.Shutdown();
        }

        private void RegisterDefaultBlocks()
        {
            BlockController.Current.RegisterBlockRules("bedrock", Block.Types.None, false);
            BlockController.Current.RegisterBlockRules("grass", Block.Types.Raw, false, (position, direction) =>
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
            BlockController.Current.RegisterBlockRules("dirt", Block.Types.Raw, false);
            BlockController.Current.RegisterBlockRules("stone", Block.Types.None, false);
            BlockController.Current.RegisterBlockRules("glass", Block.Types.Raw, true);
            BlockController.Current.RegisterBlockRules("coal_ore", Block.Types.Ore, false);
            BlockController.Current.RegisterBlockRules("gold_ore", Block.Types.Ore, false);
            BlockController.Current.RegisterBlockRules("diamond_ore", Block.Types.Ore, false);
            BlockController.Current.RegisterBlockRules("oak_log", Block.Types.None, false, (vector3, direction) =>
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
            BlockController.Current.RegisterBlockRules("oak_leaf", Block.Types.None, false);
            BlockController.Current.RegisterBlockRules("oak_leaf_apple", Block.Types.None, false);
            BlockController.Current.RegisterBlockRules("sand", Block.Types.Raw, false);
            BlockController.Current.RegisterBlockRules("water", Block.Types.Raw, true);
        }

        public void QuitToMainMenu()
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

        public static object QueueJob(Job job)
        {
            return JobExecutionQueue.QueueJob(job);
        }
    }
}
