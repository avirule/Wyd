#region

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wyd.World.Blocks;

#endregion

namespace Wyd.Controllers.State
{
    public class GameController : SingletonController<GameController>
    {
        private void Awake()
        {
            AssignSingletonInstance(this);
            DontDestroyOnLoad(this);
        }

        private void Start()
        {
            RegisterDefaultBlocks();
        }

        private static void RegisterDefaultBlocks()
        {
            BlockController.Current.RegisterBlockDefinition("bedrock", null,
                BlockDefinition.Property.Collideable);

            BlockController.Current.RegisterBlockDefinition("grass", GrassUVsRule,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("dirt", null,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("dirt_coarse", null,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("stone", null,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("glass", null,
                BlockDefinition.Property.Transparent, BlockDefinition.Property.Collectible,
                BlockDefinition.Property.Collideable, BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("coal_ore", null,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("gold_ore", null,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("diamond_ore", null,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("oak_log",
                direction =>
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
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("oak_leaf", null,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("oak_leaf_apple", null,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("sand", null,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("water", null,
                BlockDefinition.Property.Transparent);
        }

        private static string GrassUVsRule(Direction direction)
        {
            switch (direction)
            {
                case Direction.North:
                case Direction.East:
                case Direction.South:
                case Direction.West:
                case Direction.Up:
                    return "grass";
                case Direction.Down:
                    return "dirt";
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
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
    }
}
