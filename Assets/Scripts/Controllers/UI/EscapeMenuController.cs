using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Controllers.UI
{
    public class EscapeMenuController : MonoBehaviour
    {
        public Button ExitButton;

        // Start is called before the first frame update
        private void Awake()
        {
            ExitButton.onClick.AddListener(() =>
            {
                Application.Quit();

                if (Debug.isDebugBuild)
                {
                    EditorApplication.isPlaying = false;
                }
            });
        }

        private void Start()
        {
            gameObject.SetActive(false);
        }
    }
}