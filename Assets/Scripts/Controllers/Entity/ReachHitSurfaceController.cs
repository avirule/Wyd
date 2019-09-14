#region

using UnityEngine;

#endregion

namespace Controllers.Entity
{
    public class ReachHitSurfaceController : MonoBehaviour
    {
        private Material _SurfaceOverlayMaterial;
        private float _ElapsedTime;

        public Color StartColor;
        public Color EndColor;
        public float GradientTime;

        private void Awake()
        {
            _SurfaceOverlayMaterial = GetComponent<MeshRenderer>().material;
        }

        private void Update()
        {
            _ElapsedTime += Time.deltaTime;
            float ratio = _ElapsedTime / GradientTime;
            _SurfaceOverlayMaterial.color = Color.Lerp(StartColor, EndColor, ratio);

            if (_ElapsedTime >= GradientTime)
            {
                Color temp = StartColor;
                StartColor = EndColor;
                EndColor = temp;

                _ElapsedTime = 0f;
            }
        }
    }
}
