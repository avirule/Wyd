using System;
using UnityEditor.U2D;
using UnityEngine;

namespace Controllers.Graphics
{
    [RequireComponent(typeof(Camera))]
    public class WireframeCameraController : MonoBehaviour
    {
        private Camera _Camera;
        private CameraClearFlags _OriginalCameraClearFlags;
        private Color _OriginalBackgroundColor;
        private bool _PreviousMode;
        private bool _HasPressed;
        
        public Color WireframeBackgroundColor = Color.black;
        public bool IsWireframeMode;

        private void Awake()
        {
            _Camera = GetComponent<Camera>();
            _OriginalCameraClearFlags = _Camera.clearFlags;
            _OriginalBackgroundColor = _Camera.backgroundColor;
            _PreviousMode = IsWireframeMode;
            _HasPressed = false;
        }

        private void Update()
        {
            if (Input.GetKey(KeyCode.F8))
            {
                if (!_HasPressed)
                {
                    IsWireframeMode = !IsWireframeMode;
                    _HasPressed = true;
                }
            }
            else
            {
                _HasPressed = false;
            }

            if (IsWireframeMode == _PreviousMode)
            {
                return;
            }

            _PreviousMode = IsWireframeMode;

            if (IsWireframeMode)
            {
                _OriginalCameraClearFlags = _Camera.clearFlags;
                _OriginalBackgroundColor = _Camera.backgroundColor;

                _Camera.clearFlags = CameraClearFlags.Color;
                _Camera.backgroundColor = WireframeBackgroundColor;
            }
            else
            {
                _Camera.clearFlags = _OriginalCameraClearFlags;
                _Camera.backgroundColor = _OriginalBackgroundColor;
            }
        }

        private void OnPreRender()
        {
            if (IsWireframeMode)
            {
                GL.wireframe = true;
            }
        }

        private void OnPostRender()
        {
            if (IsWireframeMode)
            {
                GL.wireframe = false;
            }
        }
    }
}