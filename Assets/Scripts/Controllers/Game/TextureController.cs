#region

using System.Collections.Generic;
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

        public Texture2DArray TerrainTexture;
        public Dictionary<string, int> TextureIDs;

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

            TextureIDs = new Dictionary<string, int>();
        }

        private void Start()
        {
            ProcessTextures();
        }

        private void ProcessTextures()
        {
            Texture2D[] terrainTextures = Resources.LoadAll<Texture2D>(@"Graphics\Textures\Blocks\");

            TerrainTexture = new Texture2DArray(terrainTextures[0].width, terrainTextures[0].height,
                terrainTextures.Length,
                TextureFormat.RGBA32, true, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            for (int i = 0; i < TerrainTexture.depth; i++)
            {
                TerrainTexture.SetPixels(terrainTextures[i].GetPixels(0), i, 0);
                TextureIDs.Add(terrainTextures[i].name.ToLowerInvariant(), i);

                EventLog.Logger.Log(LogLevel.Info, $"Texture processed: {terrainTextures[i].name}");
            }

            TerrainTexture.Apply();
        }
    }
}