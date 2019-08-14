#region

using Static;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#endregion

namespace Controllers.UI
{
    public class EscapeMenuController : MonoBehaviour
    {// Start is called before the first frame update
        private void Start()
        {
            gameObject.SetActive(false);
        }
    }
}