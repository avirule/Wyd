#region

using System;
using System.Threading;
using Controllers.World;
using Game;
using Game.World.Blocks;
using NLog;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

#endregion

namespace Controllers.State
{
    public class GameController : SingletonController<GameController>
    {
        public static readonly int MainThreadId = Thread.CurrentThread.ManagedThreadId;

        private void Awake()
        {
            // todo EntityController with source/subscriber architecture
            AssignCurrent(this);
            DontDestroyOnLoad(this);
            QualitySettings.vSyncCount = 0;
        }

        private void Start()
        {
            RegisterDefaultBlocks();
        }

        private void OnApplicationQuit()
        {
            LogManager.Shutdown();
        }

        private void RegisterDefaultBlocks()
        {
            BlockController.Current.RegisterBlockRules("bedrock", false);
            BlockController.Current.RegisterBlockRules("grass", false, (position, direction) =>
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
            BlockController.Current.RegisterBlockRules("dirt", false);
            BlockController.Current.RegisterBlockRules("stone", false);
            BlockController.Current.RegisterBlockRules("glass", true);
            BlockController.Current.RegisterBlockRules("coal_ore", false);
            BlockController.Current.RegisterBlockRules("gold_ore", false);
            BlockController.Current.RegisterBlockRules("diamond_ore", false);
            BlockController.Current.RegisterBlockRules("oak_log", false, (vector3, direction) =>
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
            BlockController.Current.RegisterBlockRules("oak_leaf", false);
            BlockController.Current.RegisterBlockRules("oak_leaf_apple", false);
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
    }
}
