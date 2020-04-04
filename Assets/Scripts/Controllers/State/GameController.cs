#region

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wyd.Controllers.World;
using Wyd.Game;
using Wyd.Game.World.Blocks;
using Object = UnityEngine.Object;

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
                    Vector3 positionAbove = position + Vector3.up;
                    WorldController.Current.TryGetBlockAt(positionAbove, out ushort blockId);

                    if (!BlockController.Current.CheckBlockHasProperties(blockId, BlockDefinition.Property.Transparent))
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
    }
}
