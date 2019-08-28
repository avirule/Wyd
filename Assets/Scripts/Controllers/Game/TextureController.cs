#region

using System.Collections.Generic;
using Graphics;
using Logging;
using NLog;
using UnityEngine;

#endregion

namespace Controllers.Game
{
    public class TextureController : MonoBehaviour
    {
        public static readonly int MainTex = Shader.PropertyToID("_MainTex");

        public static TextureController Current;

        public Texture2DArray TerrainTexture { get; private set; }
        private Dictionary<string, int> _TextureIDs;

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

            _TextureIDs = new Dictionary<string, int>();
        }

        private void Start()
        {
            ProcessSprites();
        }

        private void ProcessSprites()
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(@"Graphics\Textures\Blocks\");

            TerrainTexture = new Texture2DArray((int) sprites[0].rect.width, (int) sprites[0].rect.height,
                sprites.Length, TextureFormat.RGBA32, true, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            for (int i = 0; i < TerrainTexture.depth; i++)
            {
                Color[] spritePixels = sprites[i].GetPixels();
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

            if (!_TextureIDs.ContainsKey(textureName))
            {
                return false;
            }

            textureId = _TextureIDs[textureName];
            return true;
        }
    }
}