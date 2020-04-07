#region

using System;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wyd.Controllers.World;
using Wyd.Game;
using Wyd.Game.World.Blocks;

#endregion

namespace Wyd.Controllers.State
{
    public class GameController : SingletonController<GameController>
    {
        private void Awake()
        {
            AssignSingletonInstance(this);
            DontDestroyOnLoad(this);
            QualitySettings.vSyncCount = 0;
        }

        private void Start()
        {
            RegisterDefaultBlocks();
        }

        private void RegisterDefaultBlocks()
        {
            BlockController.Current.RegisterBlockDefinition("bedrock", Block.Types.None, null,
                BlockDefinition.Property.Collideable);

            BlockController.Current.RegisterBlockDefinition("grass", Block.Types.Raw,
                (position, direction) =>
                {
                    int3 positionAbove = position + Directions.Up;
                    WorldController.Current.TryGetBlockAt(positionAbove, out ushort blockId);

                    if (!BlockController.Current.CheckBlockHasProperties(blockId, BlockDefinition.Property.Transparent))
                    {
                        return "dirt";
                    }

                    switch (direction)
                    {
                        case Direction.North:
                        case Direction.East:
                        case Direction.South:
                        case Direction.West:
                        //return "grass_side";
                        case Direction.Up:
                            return "grass";
                        case Direction.Down:
                            return "dirt";
                        default:
                            throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
                    }
                }, BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("dirt", Block.Types.Raw, null,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("stone", Block.Types.None, null,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("glass", Block.Types.Raw, null,
                BlockDefinition.Property.Transparent, BlockDefinition.Property.Collectible,
                BlockDefinition.Property.Collideable, BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("coal_ore", Block.Types.Ore, null,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("gold_ore", Block.Types.Ore, null,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("diamond_ore", Block.Types.Ore, null,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("oak_log", Block.Types.None,
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
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("oak_leaf", Block.Types.None, null,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("oak_leaf_apple", Block.Types.None, null,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("sand", Block.Types.Raw, null,
                BlockDefinition.Property.Collectible, BlockDefinition.Property.Collideable,
                BlockDefinition.Property.Destroyable);

            BlockController.Current.RegisterBlockDefinition("water", Block.Types.Raw, null,
                BlockDefinition.Property.Transparent);
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
