// GENERATED AUTOMATICALLY FROM 'Assets/Scripts/Controllers/PlayerInputActions.inputactions'

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace Wyd.Controllers
{
    public class @PlayerInputActions : IInputActionCollection, IDisposable
    {
        public InputActionAsset asset { get; }
        public @PlayerInputActions()
        {
            asset = InputActionAsset.FromJson(@"{
    ""name"": ""PlayerInputActions"",
    ""maps"": [
        {
            ""name"": ""Player Controls"",
            ""id"": ""b2b3e2b0-bf70-4bb3-b8aa-53996cc0dda1"",
            ""actions"": [
                {
                    ""name"": ""Move"",
                    ""type"": ""PassThrough"",
                    ""id"": ""3a2158d7-85c1-466d-8e08-39e2ef165edb"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Look"",
                    ""type"": ""PassThrough"",
                    ""id"": ""b8907e2e-f981-40b2-9c6d-b416ea0bf7ea"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """"
                }
            ],
            ""bindings"": [
                {
                    ""name"": ""2D Vector"",
                    ""id"": ""a80d734e-1ad7-4416-8e76-dd6036825b54"",
                    ""path"": ""2DVector"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""up"",
                    ""id"": ""7d976e12-67c9-4fac-8e01-d484ed36e86a"",
                    ""path"": ""<Keyboard>/w"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""down"",
                    ""id"": ""8127c331-50eb-4580-8629-10e4d4554592"",
                    ""path"": ""<Keyboard>/s"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""left"",
                    ""id"": ""48380bc9-1e92-4168-aded-90bc20c29780"",
                    ""path"": ""<Keyboard>/a"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""right"",
                    ""id"": ""769559ad-ae15-4ef0-bbfa-75354180fa8a"",
                    ""path"": ""<Keyboard>/d"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Move"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": """",
                    ""id"": ""7d3c968f-bef3-48c6-ac5d-95bbeac24c65"",
                    ""path"": ""<Mouse>/delta"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Look"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        }
    ],
    ""controlSchemes"": []
}");
            // Player Controls
            m_PlayerControls = asset.FindActionMap("Player Controls", throwIfNotFound: true);
            m_PlayerControls_Move = m_PlayerControls.FindAction("Move", throwIfNotFound: true);
            m_PlayerControls_Look = m_PlayerControls.FindAction("Look", throwIfNotFound: true);
        }

        public void Dispose()
        {
            UnityEngine.Object.Destroy(asset);
        }

        public InputBinding? bindingMask
        {
            get => asset.bindingMask;
            set => asset.bindingMask = value;
        }

        public ReadOnlyArray<InputDevice>? devices
        {
            get => asset.devices;
            set => asset.devices = value;
        }

        public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

        public bool Contains(InputAction action)
        {
            return asset.Contains(action);
        }

        public IEnumerator<InputAction> GetEnumerator()
        {
            return asset.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Enable()
        {
            asset.Enable();
        }

        public void Disable()
        {
            asset.Disable();
        }

        // Player Controls
        private readonly InputActionMap m_PlayerControls;
        private IPlayerControlsActions m_PlayerControlsActionsCallbackInterface;
        private readonly InputAction m_PlayerControls_Move;
        private readonly InputAction m_PlayerControls_Look;
        public struct PlayerControlsActions
        {
            private @PlayerInputActions m_Wrapper;
            public PlayerControlsActions(@PlayerInputActions wrapper) { m_Wrapper = wrapper; }
            public InputAction @Move => m_Wrapper.m_PlayerControls_Move;
            public InputAction @Look => m_Wrapper.m_PlayerControls_Look;
            public InputActionMap Get() { return m_Wrapper.m_PlayerControls; }
            public void Enable() { Get().Enable(); }
            public void Disable() { Get().Disable(); }
            public bool enabled => Get().enabled;
            public static implicit operator InputActionMap(PlayerControlsActions set) { return set.Get(); }
            public void SetCallbacks(IPlayerControlsActions instance)
            {
                if (m_Wrapper.m_PlayerControlsActionsCallbackInterface != null)
                {
                    @Move.started -= m_Wrapper.m_PlayerControlsActionsCallbackInterface.OnMove;
                    @Move.performed -= m_Wrapper.m_PlayerControlsActionsCallbackInterface.OnMove;
                    @Move.canceled -= m_Wrapper.m_PlayerControlsActionsCallbackInterface.OnMove;
                    @Look.started -= m_Wrapper.m_PlayerControlsActionsCallbackInterface.OnLook;
                    @Look.performed -= m_Wrapper.m_PlayerControlsActionsCallbackInterface.OnLook;
                    @Look.canceled -= m_Wrapper.m_PlayerControlsActionsCallbackInterface.OnLook;
                }
                m_Wrapper.m_PlayerControlsActionsCallbackInterface = instance;
                if (instance != null)
                {
                    @Move.started += instance.OnMove;
                    @Move.performed += instance.OnMove;
                    @Move.canceled += instance.OnMove;
                    @Look.started += instance.OnLook;
                    @Look.performed += instance.OnLook;
                    @Look.canceled += instance.OnLook;
                }
            }
        }
        public PlayerControlsActions @PlayerControls => new PlayerControlsActions(this);
        public interface IPlayerControlsActions
        {
            void OnMove(InputAction.CallbackContext context);
            void OnLook(InputAction.CallbackContext context);
        }
    }
}
