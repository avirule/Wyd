#region

using UnityEditor;
using UnityEngine;

#endregion

namespace Static
{
    public static class StaticMethods
    {
        public static Vector3Int ToInt(this Vector3 vector)
        {
            return new Vector3Int((int) vector.x, (int) vector.y, (int) vector.z);
        }

        public static void ApplicationClose(int errorCode = -1)
        {
            Application.Quit(errorCode);

            if (Application.isEditor)
            {
                EditorApplication.ExitPlaymode();
            }
        }
    }
}