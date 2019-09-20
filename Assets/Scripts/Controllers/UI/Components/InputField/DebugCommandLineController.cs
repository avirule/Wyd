#region

using System.Linq;
using Controllers.State;
using Controllers.World;
using Game.World.Blocks;
using Logging;
using NLog;
using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.InputField
{
    public class DebugCommandLineController : MonoBehaviour
    {
        private TMP_InputField _CommandLineInput;
        private bool _EscapeKeyPressed;

        private void Awake()
        {
            _CommandLineInput = GetComponent<TMP_InputField>();
            _CommandLineInput.text = string.Empty;
            _CommandLineInput.onSubmit.AddListener(OnSubmit);
        }

        private void Update()
        {
            bool commandLineKeyPressed = InputController.Current.GetButton("CommandLine", this);

            if (!commandLineKeyPressed && !_EscapeKeyPressed)
            {
                return;
            }

            if (commandLineKeyPressed
                && !_CommandLineInput.isFocused)
            {
                Focus(true);
            }
            else if (InputController.Current.GetKey(KeyCode.Escape, this)
                     && _CommandLineInput.isFocused)
            {
                _EscapeKeyPressed = true;
            }
            else if (!InputController.Current.GetKey(KeyCode.Escape, this)
                     && _EscapeKeyPressed)
            {
                _EscapeKeyPressed = false;
                Focus(false);
            }
        }

        private void OnSubmit(string value)
        {
            ParseCommandLineArguments(value.Split(' '));
            _CommandLineInput.text = string.Empty;
            Focus(false);
        }

        private void Focus(bool focus)
        {
            if (focus && InputController.Current.Lock(this))
            {
                _CommandLineInput.ActivateInputField();
            }
            else
            {
                _CommandLineInput.DeactivateInputField();
                InputController.Current.Unlock(this);
            }
        }

        private static void ParseCommandLineArguments(params string[] args)
        {
            if ((args.Length == 0) || string.IsNullOrWhiteSpace(args[0]))
            {
                return;
            }

            for (int i = 0; i < args.Length; i++)
            {
                args[i] = args[i].ToLower();
            }

            switch (args[0])
            {
                case "get":
                    if (args[1].Equals("block") && args[2].Equals("at"))
                    {
                        if (args.Length >= 6)
                        {
                            if (!int.TryParse(args[3], out int x)
                                || !int.TryParse(args[4], out int y)
                                || !int.TryParse(args[5], out int z))
                            {
                                return;
                            }

                            Vector3 position = new Vector3(x, y, z);

                            if (!WorldController.Current.TryGetBlockAt(position, out Block block))
                            {
                                EventLog.Logger.Log(LogLevel.Warn, $"Failed to get block at position: {position}");
                            }

                            string blockName = block.Id == BlockController.BLOCK_EMPTY_ID
                                ? "Air"
                                : BlockController.Current.GetBlockName(block.Id);

                            EventLog.Logger.Log(LogLevel.Info,
                                $"Request for block at position {position} returned `{blockName}`.");
                        }
                    }

                    break;
                case "testcompress":
                    if (args.Length < 4)
                    {
                        EventLog.Logger.Log(LogLevel.Warn, "Not enough arguments.");
                        break;
                    }

                    if (!int.TryParse(args[1], out int x1)
                        || !int.TryParse(args[2], out int y1)
                        || !int.TryParse(args[3], out int z1))
                    {
                        EventLog.Logger.Log(LogLevel.Warn, "Invalid coordinates.");
                        break;
                    }

                    Vector3 chunkPosition = new Vector3(x1, y1, z1);
                    
                    if (!WorldController.Current.TryGetChunkAt(chunkPosition,
                        out ChunkController chunkController))
                    {
                        EventLog.Logger.Log(LogLevel.Warn, $"No chunk at coordinates {chunkPosition}.");
                        break;
                    }
                    
                    EventLog.Logger.Log(LogLevel.Info, chunkController.GetCompressed().Count());
                    break;
                default:
                    EventLog.Logger.Log(LogLevel.Warn, "Command invalid.");
                    break;
            }
        }
    }
}
