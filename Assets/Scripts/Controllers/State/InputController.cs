#region

using UnityEngine;

#endregion

namespace Controllers.State
{
    public class InputController : LockableSingletonController<InputController>
    {
        private void Awake()
        {
            AssignCurrent(this);
        }

        public bool GetKey(KeyCode keyCode, object keyMaster = null)
        {
            return IsLockedFor(keyMaster) && Input.GetKey(keyCode);
        }

        public bool GetButton(string button, object keyMaster = null)
        {
            return IsLockedFor(keyMaster) && Input.GetButton(button);
        }

        public float GetAxis(string axisName, object keyMaster = null)
        {
            return IsLockedFor(keyMaster) ? Input.GetAxis(axisName) : 0f;
        }
        
        public float GetAxisRaw(string axisName, object keyMaster = null)
        {
            return IsLockedFor(keyMaster) ? Input.GetAxisRaw(axisName) : 0f;
        }
        
        public void ToggleCursorLocked(bool value, object keyMaster = null)
        {
            if (!IsLockedFor(keyMaster))
            {
                return;
            }
            
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
    }
}
