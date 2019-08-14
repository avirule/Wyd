#region

using System;
using System.Collections.Generic;
using Logging;
using NLog;
using UnityEngine;

#endregion

namespace Controllers.Game
{
    public class TextureController : MonoBehaviour
    {
        public static TextureController Current;

        public Dictionary<string, Vector2[]> Sprites;

        private void Awake()
        {
            if ((Current != null) && (Current != this))
            {
                Destroy(gameObject);
            }
            else
            {
                Current = this;
            }

            Sprites = new Dictionary<string, Vector2[]>();
        }

        private void Start()
        {
            ProcessSprites();
        }

        private void ProcessSprites()
        {
            Sprite[] terrainSprites = Resources.LoadAll<Sprite>(@"Environment\Terrain\");

            foreach (Sprite sprite in terrainSprites)
            {
                float xMinNormalized = sprite.rect.xMin / sprite.texture.width;
                float xMaxNormalized = sprite.rect.xMax / sprite.texture.width;
                float yMinNormalized = sprite.rect.yMin / sprite.texture.height;
                float yMaxNormalized = sprite.rect.yMax / sprite.texture.height;

                Vector2[] uvs =
                {
                    new Vector2(xMinNormalized, yMinNormalized),
                    new Vector2(xMaxNormalized, yMinNormalized),
                    new Vector2(xMinNormalized, yMaxNormalized),
                    new Vector2(xMaxNormalized, yMaxNormalized)
                };

                Sprites.Add(sprite.name, uvs);

                EventLog.Logger.Log(LogLevel.Info, $"Sprite processed: {sprite.name}");
            }
        }
    }
}