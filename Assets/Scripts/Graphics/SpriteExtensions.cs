using UnityEngine;

namespace Graphics
{
    public static class SpriteExtensions
    {
        public static Color[] GetPixels(this Sprite sprite)
        {
            int x = Mathf.FloorToInt(sprite.rect.x);
            int y = Mathf.FloorToInt(sprite.rect.y);
            int width = Mathf.FloorToInt(sprite.rect.width);
            int height = Mathf.FloorToInt(sprite.rect.height);
            
            return sprite.texture.GetPixels(x, y, width, height);
        }
    }
}