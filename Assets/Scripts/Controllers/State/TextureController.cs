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
        public Texture2DArray BlocksTexture { get; private set; }
        public Material BlocksMaterial { get; private set; }
        public Material TransparentBlocksMaterial { get; private set; }
        public Material[] AllBlocksMaterials { get; private set; }
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
                material.SetTexture(MainTexPropertyID, BlocksTexture);
            }

            AllBlocksMaterials = MeshRenderer.materials;
            BlocksMaterial = AllBlocksMaterials[0];
            TransparentBlocksMaterial = AllBlocksMaterials[1];
        }

        private void ProcessSprites()
        {
            Sprite[] sprites = Resources.LoadAll<Sprite>(@"Graphics/Textures/Blocks/");

            BlocksTexture = new Texture2DArray((int)sprites[0].rect.width, (int)sprites[0].rect.height,
                sprites.Length, TextureFormat.RGBA32, true, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            for (int depth = 0; depth < BlocksTexture.depth; depth++)
            {
                Color[] spritePixels = GetPixels(sprites[depth]);
                BlocksTexture.SetPixels(spritePixels, depth, 0);
                _TextureIDs.Add(sprites[depth].name.ToLower(), depth);

                Log.Information($"Texture processed (depth {depth}): {sprites[depth].name}");
            }

            BlocksTexture.Apply();
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
