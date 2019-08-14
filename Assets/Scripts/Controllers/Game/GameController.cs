#region

using System;
using System.Threading.Tasks;
using Controllers.World;
using Environment;
using Environment.Terrain;
using NLog;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

#endregion

namespace Controllers.Game
{
    public class GameController : MonoBehaviour
    {
        public static GameController Current;
        public static OptionsController Options;

        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Current = this;
            }
         
            DontDestroyOnLoad(this);
            ToggleCursorLocked(true);
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

        private void Initialise()
        {
        }

        private void RegisterDefaultBlocks()
        {
            BlockController.Current.RegisterBlockRules("Grass", false, (position, direction) =>
            {
                Vector3Int positionAbove = position + Vector3Int.up;
                Block blockAbove = WorldController.Current.GetBlockAtPosition(positionAbove);

                if (!blockAbove.Transparent)
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
            BlockController.Current.RegisterBlockRules("Dirt", true);
            BlockController.Current.RegisterBlockRules("Stone", true);
            BlockController.Current.RegisterBlockRules("Glass", true);
            BlockController.Current.RegisterBlockRules("CoalOre", true);
            BlockController.Current.RegisterBlockRules("GoldOre", true);
            BlockController.Current.RegisterBlockRules("DiamondOre", true);
        }

        public void ToggleCursorLocked(bool value)
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
        
        public void QuitToMainMenu()
        {
            SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
        }
        
        public void ApplicationClose(int exitCode = -1)
        {
            Application.Quit(exitCode);

#if UNITY_EDITOR
            EditorApplication.ExitPlaymode();
#endif
        }
    }
}