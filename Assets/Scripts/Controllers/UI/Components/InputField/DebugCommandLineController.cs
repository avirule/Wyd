#region

using Controllers.Game;
using Controllers.World;
using Game.Terrain;
using Logging;
using NLog;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

#endregion

namespace Controllers.UI.Components.InputField
{
    public class DebugCommandLineController : MonoBehaviour
    {
        private TMP_InputField _CommandLineInput;

        private void Awake()
        {
            _CommandLineInput = GetComponent<TMP_InputField>();
            _CommandLineInput.text = string.Empty;
            _CommandLineInput.onSubmit.AddListener(OnSubmit);
        }

        private void Update()
        {
            if (Input.GetButton("CommandLine") && !_CommandLineInput.isFocused)
            {
                Focus(true);
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
            if (focus)
            {
                EventSystem.current.SetSelectedGameObject(_CommandLineInput.gameObject, null);
                _CommandLineInput.OnPointerClick(null);
            }
            else
            {
                EventSystem.current.SetSelectedGameObject(null, null);
            }
        }

        private void ParseCommandLineArguments(params string[] args)
        {
            switch (args[0])
            {
                case "get":
                    if (args[1].Equals("block") && args[2].Equals("at"))
                    {
                        if (args.Length >= 6)
                        {
                            if (!int.TryParse(args[3], out int x) ||
                                !int.TryParse(args[4], out int y) ||
                                !int.TryParse(args[5], out int z))
                            {
                                return;
                            }

                            ushort blockId = WorldController.Current.GetBlockAtPosition(new Vector3Int(x, y, z));

                            string blockName = blockId == BlockController.BLOCK_EMPTY_ID
                                ? "Air"
                                : BlockController.Current.GetBlockName(blockId);

                            EventLog.Logger.Log(LogLevel.Info,
                                $"Request for block at position ({x}, {y}, {z}) returned `{blockName}`.");
                        }
                    }

                    break;
            }
        }
    }
}