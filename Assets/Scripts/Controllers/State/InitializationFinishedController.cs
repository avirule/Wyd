#region

using UnityEngine;
using UnityEngine.SceneManagement;

#endregion

namespace Wyd.Controllers.State
{
    public class InitializationFinishedController : MonoBehaviour
    {
        private void Start()
        {
            SceneManager.LoadSceneAsync("Scenes/MainMenu", LoadSceneMode.Single);
        }
    }
}
