#region

using TMPro;
using UnityEngine;

#endregion

namespace Wyd.Controllers.UI.Components.Text
{
    public class FormattedTextController : MonoBehaviour
    {
        protected TextMeshProUGUI TextObject { get; private set; }
        protected string Format { get; private set; }

        protected virtual void Awake()
        {
            TextObject = GetComponent<TextMeshProUGUI>();
            Format = TextObject.text;
        }
    }
}
