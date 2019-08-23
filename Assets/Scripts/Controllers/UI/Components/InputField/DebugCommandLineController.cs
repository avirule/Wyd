using System;
using TMPro;
using UnityEditor.U2D;
using UnityEngine;

namespace Controllers.UI.Components.InputField
{
    public class DebugCommandLineController : MonoBehaviour
    {
        private TMP_InputField _CommandLineInput;

        private void Awake()
        {
            _CommandLineInput = GetComponent<TMP_InputField>();
            _CommandLineInput.text = string.Empty;
        }
    }
}