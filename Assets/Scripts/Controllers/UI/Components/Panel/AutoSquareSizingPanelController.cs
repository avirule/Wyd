#region

using UnityEngine;

#endregion

namespace Controllers.UI.Components.Panel
{
    public class AutoSquareSizingPanelController : MonoBehaviour
    {
        private RectTransform _RectTransform;
        private Vector2Int _LastScreenSize;
        private int _SkippedFrames;

        public RectTransform.Axis SquaringAxis;
        public int SkipFrames;

        private void Awake()
        {
            _RectTransform = GetComponent<RectTransform>();
        }

        private void Update()
        {
            if (_SkippedFrames > SkipFrames)
            {
                _SkippedFrames = 0;
            }
            else
            {
                _SkippedFrames += 1;
                return;
            }

            if ((_LastScreenSize.x != Screen.width) || (_LastScreenSize.y != Screen.height))
            {
                Vector2 sizeDelta = _RectTransform.sizeDelta;
                Rect rect = _RectTransform.rect;

                switch (SquaringAxis)
                {
                    case RectTransform.Axis.Horizontal:
                        _RectTransform.sizeDelta = new Vector2(rect.height, sizeDelta.y);
                        break;
                    case RectTransform.Axis.Vertical:
                        _RectTransform.sizeDelta = new Vector2(sizeDelta.x, rect.width);
                        break;
                }

                _LastScreenSize = new Vector2Int(Screen.width, Screen.height);
            }
        }
    }
}
