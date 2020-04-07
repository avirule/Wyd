#region

using TMPro;
using UnityEngine;

#endregion

namespace Wyd.Controllers.UI.Components.Text.Diagnostic
{
    public class VersionTextController : MonoBehaviour
    {
        private TextMeshProUGUI _VersionText;

        private void Awake()
        {
            _VersionText = GetComponent<TextMeshProUGUI>();
            _VersionText.text = Application.version;
        }
    }
}
