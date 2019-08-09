using UnityEditor;
using UnityEngine;

namespace Static
{
    public static class StaticMethods
    {
        public static void CreateArray(ref float[][] arr, Vector3Int size)
        {
            arr = new float[size.x][];

            for (int x = 0; x < size.x; x++)
            {
                arr[x] = new float[size.z];
            }
        }

        public static Vector3Int ToInt(this Vector3 vector)
        {
            return new Vector3Int((int) vector.x, (int) vector.y, (int) vector.z);
        }

        public static void ApplicationClose(int errorCode = -1)
        {
            Application.Quit(errorCode);

#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#endif
        }
    }
}