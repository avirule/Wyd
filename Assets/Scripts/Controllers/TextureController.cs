using System.Collections.Generic;
using Logging;
using NLog;
using UnityEngine;

namespace Controllers
{
    public class TextureController : MonoBehaviour
    {
        public bool Initialised;
        public Dictionary<string, Vector2[]> Sprites;

        private void Awake()
        {
            Initialised = false;
            Sprites = new Dictionary<string, Vector2[]>();
        }

        public void Initialise()
        {
            if (Initialised)
            {
                return;
            }

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