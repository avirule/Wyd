#region

using System;
using UnityEngine;
using UnityEngine.UI;
using Random = System.Random;

#endregion

namespace Tayx.Graphy.CustomizationScene
{
    public class G_CUIColorPicker : MonoBehaviour
    {
        public Color Color
        {
            get => _color;
            set => Setup(value);
        }

        public void SetOnValueChangeCallback(Action<Color> onValueChange)
        {
            _onValueChange = onValueChange;
        }

        [SerializeField]
        private Slider alphaSlider;

        [SerializeField]
        private Image alphaSliderBGImage;

        private Color _color = new Color32(255, 0, 0, 128);
        private Action<Color> _onValueChange;
        private Action _update;

        private static void RGBToHSV(Color color, out float h, out float s, out float v)
        {
            float cmin = Mathf.Min(color.r, color.g, color.b);
            float cmax = Mathf.Max(color.r, color.g, color.b);
            float d = cmax - cmin;
            if (d == 0)
            {
                h = 0;
            }
            else if (cmax == color.r)
            {
                h = Mathf.Repeat((color.g - color.b) / d, 6);
            }
            else if (cmax == color.g)
            {
                h = ((color.b - color.r) / d) + 2;
            }
            else
            {
                h = ((color.r - color.g) / d) + 4;
            }

            s = cmax == 0 ? 0 : d / cmax;
            v = cmax;
        }

        private static bool GetLocalMouse(GameObject go, out Vector2 result)
        {
            RectTransform rt = (RectTransform) go.transform;
            Vector3 mp = rt.InverseTransformPoint(Input.mousePosition);
            result.x = Mathf.Clamp(mp.x, rt.rect.min.x, rt.rect.max.x);
            result.y = Mathf.Clamp(mp.y, rt.rect.min.y, rt.rect.max.y);
            return rt.rect.Contains(mp);
        }

        private static Vector2 GetWidgetSize(GameObject go)
        {
            RectTransform rt = (RectTransform) go.transform;
            return rt.rect.size;
        }

        private GameObject GO(string name) => transform.Find(name).gameObject;

        private void Setup(Color inputColor)
        {
            alphaSlider.value = inputColor.a;
            alphaSliderBGImage.color = inputColor;

            GameObject satvalGO = GO("SaturationValue");
            GameObject satvalKnob = GO("SaturationValue/Knob");
            GameObject hueGO = GO("Hue");
            GameObject hueKnob = GO("Hue/Knob");
            GameObject result = GO("Result");
            Color[] hueColors =
            {
                Color.red,
                Color.yellow,
                Color.green,
                Color.cyan,
                Color.blue,
                Color.magenta
            };

            Color[] satvalColors =
            {
                new Color(0, 0, 0),
                new Color(0, 0, 0),
                new Color(1, 1, 1),
                hueColors[0]
            };

            Texture2D hueTex = new Texture2D(1, 7);

            for (int i = 0; i < 7; i++)
            {
                hueTex.SetPixel(0, i, hueColors[i % 6]);
            }

            hueTex.Apply();
            hueGO.GetComponent<Image>().sprite =
                Sprite.Create(hueTex, new Rect(0, 0.5f, 1, 6), new Vector2(0.5f, 0.5f));
            Vector2 hueSz = GetWidgetSize(hueGO);
            Texture2D satvalTex = new Texture2D(2, 2);
            satvalGO.GetComponent<Image>().sprite =
                Sprite.Create(satvalTex, new Rect(0.5f, 0.5f, 1, 1), new Vector2(0.5f, 0.5f));

            Action resetSatValTexture = () =>
            {
                for (int j = 0; j < 2; j++)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        satvalTex.SetPixel(i, j, satvalColors[i + (j * 2)]);
                    }
                }

                satvalTex.Apply();
            };

            Vector2 satvalSz = GetWidgetSize(satvalGO);
            float Hue, Saturation, Value;
            RGBToHSV(inputColor, out Hue, out Saturation, out Value);

            Action applyHue = () =>
            {
                int i0 = Mathf.Clamp((int) Hue, 0, 5);
                int i1 = (i0 + 1) % 6;
                Color resultColor = Color.Lerp(hueColors[i0], hueColors[i1], Hue - i0);
                satvalColors[3] = resultColor;
                resetSatValTexture();
            };

            Action applySaturationValue = () =>
            {
                Vector2 sv = new Vector2(Saturation, Value);
                Vector2 isv = new Vector2(1 - sv.x, 1 - sv.y);
                Color c0 = isv.x * isv.y * satvalColors[0];
                Color c1 = sv.x * isv.y * satvalColors[1];
                Color c2 = isv.x * sv.y * satvalColors[2];
                Color c3 = sv.x * sv.y * satvalColors[3];
                Color resultColor = c0 + c1 + c2 + c3;
                Image resImg = result.GetComponent<Image>();
                resImg.color = resultColor;
                if (_color != resultColor)
                {
                    resultColor = new Color(resultColor.r, resultColor.g, resultColor.b, alphaSlider.value);

                    if (_onValueChange != null)
                    {
                        _onValueChange(resultColor);
                    }

                    _color = resultColor;

                    alphaSliderBGImage.color = _color;
                }
            };
            applyHue();
            applySaturationValue();
            satvalKnob.transform.localPosition = new Vector2(Saturation * satvalSz.x, Value * satvalSz.y);
            hueKnob.transform.localPosition = new Vector2(hueKnob.transform.localPosition.x, (Hue / 6) * satvalSz.y);
            Action dragH = null;
            Action dragSV = null;
            Action idle = () =>
            {
                if (Input.GetMouseButtonDown(0))
                {
                    Vector2 mp;
                    if (GetLocalMouse(hueGO, out mp))
                    {
                        _update = dragH;
                    }
                    else if (GetLocalMouse(satvalGO, out mp))
                    {
                        _update = dragSV;
                    }
                }
            };
            dragH = () =>
            {
                Vector2 mp;
                GetLocalMouse(hueGO, out mp);
                Hue = (mp.y / hueSz.y) * 6;
                applyHue();
                applySaturationValue();
                hueKnob.transform.localPosition = new Vector2(hueKnob.transform.localPosition.x, mp.y);
                if (Input.GetMouseButtonUp(0))
                {
                    _update = idle;
                }
            };
            dragSV = () =>
            {
                Vector2 mp;
                GetLocalMouse(satvalGO, out mp);
                Saturation = mp.x / satvalSz.x;
                Value = mp.y / satvalSz.y;
                applySaturationValue();
                satvalKnob.transform.localPosition = mp;
                if (Input.GetMouseButtonUp(0))
                {
                    _update = idle;
                }
            };
            _update = idle;
        }

        public void SetRandomColor()
        {
            Random rng = new Random();
            float r = (rng.Next() % 1000) / 1000.0f;
            float g = (rng.Next() % 1000) / 1000.0f;
            float b = (rng.Next() % 1000) / 1000.0f;
            Color = new Color(r, g, b);
        }

        private void Awake()
        {
            Color = new Color32(255, 0, 0, 128);
        }

        private void Start()
        {
            alphaSlider.onValueChanged.AddListener(value =>
            {
                _color = new Color(_color.r, _color.g, _color.b, value);

                alphaSliderBGImage.color = _color;

                if (_onValueChange != null)
                {
                    _onValueChange(_color);
                }
            });
        }

        private void Update()
        {
            if (_update != null)
            {
                _update();
            }
        }
    }
}
