#region

using Controllers.Game;
using Game.World.Chunk;
using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
{
    public class ThreadingModeTextController : MonoBehaviour
    {
        private TextMeshProUGUI _ThreadingModeText;
        private ThreadingMode _LastThreadingMode;

        private void Awake()
        {
            _ThreadingModeText = GetComponent<TextMeshProUGUI>();
        }

        private void Start()
        {
            UpdateThreadingModeText();
        }

        private void Update()
        {
            if (OptionsController.Current.ThreadingMode != _LastThreadingMode)
            {
                UpdateThreadingModeText();
            }
        }

        private void UpdateThreadingModeText()
        {
            _LastThreadingMode = OptionsController.Current.ThreadingMode;

            _ThreadingModeText.text = $"Threading Mode: {_LastThreadingMode.ToString()}";
        }
    }
}