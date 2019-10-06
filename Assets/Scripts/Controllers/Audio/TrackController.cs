#region

using System.Collections;
using UnityEngine;
using Wyd.Audio;

#endregion

namespace Wyd.Controllers.Audio
{
    public enum TrackControllerDomain
    {
        MainMenu,
        GameWorld
    }

    public class TrackController : MonoBehaviour
    {
        private bool _ReplayTimerActive;

        public AudioSource AudioSource;
        public TrackControllerDomain Domain;
        public float SecondsUntilReplay;

        // Start is called before the first frame update
        private void Start()
        {
            if (Domain == TrackControllerDomain.MainMenu)
            {
                StartCoroutine(AudioFade.FadeIn(AudioSource, 0.2f));
            }
        }

        private void Update()
        {
            if (!_ReplayTimerActive && !AudioSource.isPlaying && (SecondsUntilReplay != -1f))
            {
                StartCoroutine(SimpleCounter());
            }
        }

        private IEnumerator SimpleCounter()
        {
            if (_ReplayTimerActive)
            {
                yield break;
            }

            _ReplayTimerActive = true;
            float secondsPassed = 0f;

            while (secondsPassed < SecondsUntilReplay)
            {
                secondsPassed += Time.deltaTime;
                yield return null;
            }

            _ReplayTimerActive = false;
            AudioSource.Play();
        }
    }
}
