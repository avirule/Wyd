#region

using System;
using Serilog;
using TMPro;
using UnityEngine;
using Wyd.Controllers.State;
using Wyd.Controllers.World;
using Wyd.Controllers.World.Chunk;

#endregion

namespace Wyd.Controllers.UI.Components.InputField
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
                // case "disablegravity":
                //     GameObject.FindWithTag("player").GetComponent<Rigidbody>().useGravity = true;
                //     break;
                case "set":
                    if ((args.Length >= 4)
                        && args[1].Equals("resolution", StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(args[2], out int width)
                        && int.TryParse(args[3], out int height))
                    {
                        if (args.Length >= 5)
                        {
                            if (bool.TryParse(args[4], out bool fullscreen))
                            {
                                Screen.SetResolution(width, height, fullscreen);
                            }
                            else if (Enum.TryParse(args[4], out FullScreenMode fullScreenMode))
                            {
                                Screen.SetResolution(width, height, fullScreenMode);
                            }
                            else
                            {
                                Screen.SetResolution(width, height, Screen.fullScreenMode);
                            }
                        }

                        Log.Information(
                            $"Screen resolution set to (w{width}, h{height}) with fullscreen mode '{Screen.fullScreenMode}'");
                    }

                    break;
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

                            if (!WorldController.Current.TryGetBlockAt(position, out ushort blockId))
                            {
                                Log.Warning($"Failed to get block at position: {position}");
                            }

                            string blockName = BlockController.Current.GetBlockName(blockId);

                            Log.Information($"Request for block at position {position} returned `{blockName}`.");
                        }
                    }

                    break;
                case "testcompress":
                    if (args.Length < 4)
                    {
                        Log.Warning("Not enough arguments.");
                        break;
                    }

                    if (!int.TryParse(args[1], out int x1)
                        || !int.TryParse(args[2], out int y1)
                        || !int.TryParse(args[3], out int z1))
                    {
                        Log.Warning("Invalid coordinates.");
                        break;
                    }

                    Vector3 chunkPosition = new Vector3(x1, y1, z1);

                    if (!WorldController.Current.TryGetChunkAt(chunkPosition,
                        out ChunkController chunkController))
                    {
                        Log.Warning($"No chunk at coordinates {chunkPosition}.");
                    }

                    //Log.Information(chunkController.GetC().Count().ToString());
                    break;
                case "testsave":
                    if (args.Length < 4)
                    {
                        Log.Warning("Not enough arguments.");
                        break;
                    }

                    if (!int.TryParse(args[1], out int x2)
                        || !int.TryParse(args[2], out int y2)
                        || !int.TryParse(args[3], out int z2))
                    {
                        Log.Warning("Invalid coordinates.");
                        break;
                    }

                    Vector3 chunkPosition2 = new Vector3(x2, y2, z2);

                    if (!WorldController.Current.TryGetChunkAt(chunkPosition2, out ChunkController _))
                    {
                        Log.Warning($"No chunk at coordinates {chunkPosition2}.");
                    }

                    //WorldController.Current._SaveFileProvider.CompressAndCommitThreaded(chunkPosition2,
                    //    chunkController2.Serialize());
                    break;
                case "load":
                    //WorldController.Current._SaveFileProvider.TryGetSavedDataFromPosition(Vector3.zero, out byte[] data);
                    break;
                default:
                    Log.Warning("Command invalid.");
                    break;
            }
        }
    }
}
