#region

using System;
using Controllers.World;
using Environment;
using Environment.Terrain;
using NLog;
using UnityEngine;

#endregion

namespace Controllers.Game
{
    public class GameController : MonoBehaviour
    {
        public static GameSettingsController SettingsController;

        public BlockController BlockController;
        public TextureController TextureController;
        public WorldController WorldController;
        public GameSettingsController GameSettingsController;

        private void Awake()
        {
            SettingsController = GameSettingsController;
            ToggleCursorLocked(true);
            QualitySettings.vSyncCount = 0;
        }

        private void Start()
        {
            TextureController.Initialise();
            Initialise();
        }

        private void OnApplicationQuit()
        {
            LogManager.Shutdown();
        }

        private void Initialise()
        {
            RegisterDefaultBlocks();
        }

        private void RegisterDefaultBlocks()
        {
            BlockController.RegisterBlockRules("Grass", true, false, (position, direction) =>
            {
                Vector3Int positionAbove = position + Vector3Int.up;
                Block blockAbove = WorldController.GetBlockAtPosition(positionAbove);

                if (blockAbove.Opaque)
                {
                    return "Dirt";
                }

                switch (direction)
                {
                    case Direction.None:
                        return string.Empty;
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
                        return "GrassSide";
                    case Direction.Up:
                        return "Grass";
                    case Direction.Down:
                        return "Dirt";
                    default:
                        throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
                }
            });
            BlockController.RegisterBlockRules("Dirt", true, false);
            BlockController.RegisterBlockRules("Stone", true, false);
            BlockController.RegisterBlockRules("Glass", true, true);
            BlockController.RegisterBlockRules("CoalOre", true, false);
            BlockController.RegisterBlockRules("GoldOre", true, false);
            BlockController.RegisterBlockRules("DiamondOre", true, false);
        }

        public static void ToggleCursorLocked(bool value)
        {
            if (value)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}