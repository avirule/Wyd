#region

using TMPro;
using UnityEngine;

#endregion

namespace Controllers.UI.Components.Text
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
