using Static;
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
            ExitButton.onClick.AddListener(() => StaticMethods.ApplicationClose());
        }

        private void Start()
        {
            gameObject.SetActive(false);
        }
    }
}