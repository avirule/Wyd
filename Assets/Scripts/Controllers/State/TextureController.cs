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

        private static readonly int _MainTexPropertyID = Shader.PropertyToID("_MainTex");
        private static readonly int _ColorPropertyID = Shader.PropertyToID("_Color");

        private Dictionary<string, ushort> _TextureIDs;
        private Color _Color;
        private double _TimeOfDay;
        private bool _IsNightTime;
        private double _SecondsInCycle;

        private Texture2DArray BlocksTexture { get; set; }

        /// <summary>
        ///     Array storing all materials for blocks.
        /// </summary>
        /// <remarks>
        ///     [0] is for opaque blocks
        ///     [1] is for transparent blocks
        /// </remarks>
        public Material[] BlockMaterials { get; private set; }

        private void Awake()
        {
            AssignSingletonInstance(this);

            _TextureIDs = new Dictionary<string, ushort>();

            _Color = Color.white;
            _SecondsInCycle = 5d;
        }

        private void Start()
        {
            ProcessSprites();

            foreach (Material material in MeshRenderer.materials)
            {
                material.SetTexture(_MainTexPropertyID, BlocksTexture);
            }

            BlockMaterials = MeshRenderer.materials;
        }

        private Color _Daytime = new Color(0.9f, 0.9f, 0.9f, 1.0f);
        private Color _Nighttime = new Color(0.2f, 0.2f, 0.2f, 1.0f);

        private void Update()
        {
            if (_TimeOfDay < 0d)
            {
                _IsNightTime = false;
            }
            else if (_TimeOfDay > _SecondsInCycle)
            {
                _IsNightTime = true;
            }

            double timeAdjustment = Time.deltaTime / _SecondsInCycle;
            _TimeOfDay += _IsNightTime ? -timeAdjustment : timeAdjustment;

            BlockMaterials[0].SetColor(_ColorPropertyID, Color.Lerp(_Nighttime, _Daytime, (float)(_TimeOfDay / _SecondsInCycle)));
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

            for (ushort depth = 0; depth < BlocksTexture.depth; depth++)
            {
                Color[] spritePixels = GetPixels(sprites[depth]);
                BlocksTexture.SetPixels(spritePixels, depth, 0);
                _TextureIDs.Add(sprites[depth].name.ToLower(), depth);

                Log.Information($"[{nameof(TextureController)}] Processed depth {depth}: '{sprites[depth].name}'");
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

        public bool TryGetTextureId(string textureName, out ushort textureId) =>
            _TextureIDs.TryGetValue(textureName, out textureId);
    }
}
