#region

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

#endregion

namespace Wyd.Controllers.UI
{
    public class OptionsMenuController : MonoBehaviour
    {
        private Dictionary<string, GameObject> _TabButtons;
        private Dictionary<string, GameObject> _Panels;
        private string _LastPanelOpened;

        private void Awake()
        {
            _TabButtons = new Dictionary<string, GameObject>();
            _Panels = new Dictionary<string, GameObject>();

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);

                if (child.tag.Equals("MenuPanel", StringComparison.OrdinalIgnoreCase))
                {
                    _Panels.Add(child.name, child.gameObject);
                }
                else if (child.tag.Equals("TabButton", StringComparison.OrdinalIgnoreCase))
                {
                    _TabButtons.Add(child.name, child.gameObject);
                }
            }

            _LastPanelOpened = "GeneralOptions";

            // disable all objs
            DisablePanelsExcept(null);
            UnstyleTabButtonsExcept(null);

            SetPanelActive(_LastPanelOpened);
        }

        public void SetPanelActive(string panelName)
        {
            if (!_Panels.TryGetValue(panelName, out GameObject panel))
            {
                return;
            }

            panel.SetActive(true);
            _LastPanelOpened = panelName;
            DisablePanelsExcept(panel);
            UnderlineTabButton(panelName);
        }

        private void UnderlineTabButton(string tabButtonName)
        {
            if (!_TabButtons.TryGetValue(tabButtonName, out GameObject tabButton))
            {
                return;
            }

            tabButton.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Underline;
            UnstyleTabButtonsExcept(tabButton);
        }

        private void UnstyleTabButtonsExcept(Object obj)
        {
            foreach (GameObject tabButton in _TabButtons.Values.Where(tabButton => tabButton != obj))
            {
                tabButton.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Normal;
            }
        }

        private void DisablePanelsExcept(Object obj)
        {
            foreach (GameObject panel in _Panels.Values.Where(panel => panel != obj))
            {
                panel.SetActive(false);
            }
        }
    }
}
