#region

using UnityEngine;

#endregion

public class TestScript : MonoBehaviour
{
    private static Vector3 v = new Vector3(1.4f, -1.5f, -1000.30033f);

    private void Update()
    {
        Vector3 v2 = v.FloorToInt();
    }
}
