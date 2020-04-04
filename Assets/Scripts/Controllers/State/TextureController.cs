#region

using System.Collections.Generic;
using Serilog;
using UnityEngine;

#endregion

namespace Wyd.Controllers.State
{
    public class TextureController : SingletonController<TextureController>
    {
        public MeshRenderer MeshRenderer;

        public static int MainTexPropertyID => Shader.PropertyToID("_MainTex");
        public Texture2DArray TerrainTexture { get; private set; }
        public Material[] TerrainMaterials { get; private set; }
        private Dictionary<string, int> _TextureIDs;

        private void Awake()
        {
            AssignSingletonInstance(this);

            _TextureIDs = new Dictionary<string, int>();
        }

        private void Start()
        {
            ProcessSprites();

            foreach (Material material in MeshRenderer.materials)
            {
                material.SetTexture(MainTexPropertyID, TerrainTexture);
            }

            TerrainMaterials = MeshRenderer.materials;
        }

        private void ProcessSprites()
        {
            Sprite[] sprites = GameController.LoadAllResources<Sprite>(@"Graphics/Textures/Blocks/");

            TerrainTexture = new Texture2DArray((int)sprites[0].rect.width, (int)sprites[0].rect.height,
                sprites.Length, TextureFormat.RGBA32, true, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            for (int i = 0; i < TerrainTexture.depth; i++)
            {
                Color[] spritePixels = GetPixels(sprites[i]);
                TerrainTexture.SetPixels(spritePixels, i, 0);
                _TextureIDs.Add(sprites[i].name.ToLower(), i);

                Log.Information($"Texture processed (depth {i}): {sprites[i].name}");
            }

            TerrainTexture.Apply();
        }

        private static Color[] GetPixels(Sprite sprite)
        {
            int x = Mathf.FloorToInt(sprite.rect.x);
            int y = Mathf.FloorToInt(sprite.rect.y);
            int width = Mathf.FloorToInt(sprite.rect.width);
            int height = Mathf.FloorToInt(sprite.rect.height);

            return sprite.texture.GetPixels(x, y, width, height);
        }

        public bool TryGetTextureId(string textureName, out int textureId) =>
            _TextureIDs.TryGetValue(textureName, out textureId);
    }
}
