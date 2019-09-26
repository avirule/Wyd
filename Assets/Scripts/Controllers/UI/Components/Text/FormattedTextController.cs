#region

using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
{
    public class FormattedTextController : MonoBehaviour
    {
        protected TextMeshProUGUI _TextObject { get; private set; }
        protected string _Format { get; private set; }

        protected virtual void Awake()
        {
            _TextObject = GetComponent<TextMeshProUGUI>();
            _Format = _TextObject.text;
        }
    }
}
