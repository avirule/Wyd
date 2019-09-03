#region

using System.Collections.Generic;
using Logging;
using NLog;
using UnityEngine;

#endregion

namespace Controllers.Game
{
    public class TextureController : SingletonController<TextureController>
    {
        public readonly int MainTex = Shader.PropertyToID("_MainTex");
        public Texture2DArray TerrainTexture { get; private set; }
        private Dictionary<string, int> _TextureIDs;

        private void Awake()
        {
            AssignCurrent(this);

            _TextureIDs = new Dictionary<string, int>();
        }

        private void Start()
        {
            ProcessSprites();
        }

        private void ProcessSprites()
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(@"Graphics/Textures/Blocks/");

            TerrainTexture = new Texture2DArray((int) sprites[0].rect.width, (int) sprites[0].rect.height,
                sprites.Length, TextureFormat.RGBA32, true, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            for (int i = 0; i < TerrainTexture.depth; i++)
            {
                Color[] spritePixels = GetPixels(sprites[i]);
                TerrainTexture.SetPixels(spritePixels, i, 0);
                _TextureIDs.Add(sprites[i].name.ToLowerInvariant(), i);

                EventLog.Logger.Log(LogLevel.Info, $"Texture processed: {sprites[i].name}");
            }

            TerrainTexture.Apply();
        }

        public bool TryGetTextureId(string textureName, out int textureId)
        {
            textureName = textureName.ToLowerInvariant();
            textureId = -1;

            return _TextureIDs.TryGetValue(textureName, out textureId);
        }

        public static Color[] GetPixels(Sprite sprite)
        {
            int x = Mathf.FloorToInt(sprite.rect.x);
            int y = Mathf.FloorToInt(sprite.rect.y);
            int width = Mathf.FloorToInt(sprite.rect.width);
            int height = Mathf.FloorToInt(sprite.rect.height);

            return sprite.texture.GetPixels(x, y, width, height);
        }
    }
}