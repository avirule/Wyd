using System;
using Controllers.Game;
using Game.World;
using TMPro;
using UnityEngine;

namespace Controllers.UI.Components.Text
{
    public class ThreadingModeTextController : MonoBehaviour
    {
        private TextMeshProUGUI _ThreadingModeText;
        private ThreadingMode _LastThreadingMode;

        private void Awake()
        {
            _ThreadingModeText = GetComponent<TextMeshProUGUI>();

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

            _ThreadingModeText.text = _LastThreadingMode.ToString();
        }
    }
}