#region

using UnityEngine;

#endregion

namespace Wyd.Controllers.UI.Components.Layout
{
    [AddComponentMenu("UI/StackPanel")]
    public class StackPanel : MonoBehaviour
    {
        public enum StackDirection
        {
            Horizontal,
            Vertical
        }

        public Vector2Int Offset;
        public Vector2 Margin;
        public StackDirection StackingDirection;

        private void OnValidate()
        {
            Transform self = transform;
            float sectionSize;

            switch (StackingDirection)
            {
                case StackDirection.Horizontal:
                {
                    sectionSize = (Screen.width * (1f - Margin.x)) / self.childCount;

                    for (int i = 0; i < self.childCount; i++)
                    {
                        Vector2 newPos = Vector2.right * ((sectionSize * i) - sectionSize);
                        newPos -= Offset;

                        self.GetChild(i).localPosition = newPos;
                    }

                    break;
                }
                case StackDirection.Vertical:
                {
                    sectionSize = (Screen.height * (1f - Margin.y)) / self.childCount;

                    for (int i = 0; i < self.childCount; i++)
                    {
                        Vector2 newPos = Vector2.up * ((sectionSize * i) - sectionSize);
                        newPos -= Offset;

                        self.GetChild(i).localPosition = newPos;
                    }

                    break;
                }
            }
        }
    }
}
