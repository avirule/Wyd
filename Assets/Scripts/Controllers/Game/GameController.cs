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
        public BlockController BlockController;
        public TextureController TextureController;
        public WorldController WorldController;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.Locked;
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
            BlockController.RegisterBlockRules(1, "Grass", true, false, (position, direction) =>
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
                        return "Grass_Side";
                    case Direction.Up:
                        return "Grass";
                    case Direction.Down:
                        return "Dirt";
                    default:
                        throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
                }
            });
            BlockController.RegisterBlockRules(2, "Dirt", true, false, (position, direction) => "Dirt");
            BlockController.RegisterBlockRules(3, "Stone", true, false, (position, direction) => "Stone");
            BlockController.RegisterBlockRules(4, "Glass", true, true, (position, direction) => "Glass");
        }
    }
}