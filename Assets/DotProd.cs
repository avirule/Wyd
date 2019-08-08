using UnityEngine;

[ExecuteInEditMode]
public class DotProd : MonoBehaviour
{
    public Transform cube02;
    public Vector3 cube02Rotation;
    public Vector3 cubeRotation;
    public float dotProd;

    // Start is called before the first frame update
    private void Start()
    {
    }

    // Update is called once per frame
    private void Update()
    {
        cubeRotation = transform.position;
        cube02Rotation = cube02.transform.position;

        dotProd = Vector3.Dot(cubeRotation, cube02Rotation);
    }
}