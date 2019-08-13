#region

using System.Collections;
using UnityEngine;

#endregion

namespace Audio
{
    public enum FadeType
    {
        FadeIn,
        FadeOut
    }

    public class AudioFade : MonoBehaviour
    {
        private AudioSource _AudioSource;

        public float InitialVolume;
        public float MaxVolume;
        public float FadeTime;
        public FadeType FadeType;

        private void Awake()
        {
            _AudioSource = GetComponent<AudioSource>();
            _AudioSource.volume = InitialVolume;
        }

        // Update is called once per frame
        private void Start()
        {
            if (FadeType == FadeType.FadeIn)
            {
                StartCoroutine(FadeIn());
            }
        }

        private IEnumerator FadeIn()
        {
            while (_AudioSource.volume < MaxVolume)
            {
                Mathf.Clamp01(_AudioSource.volume += 1f * Time.deltaTime * FadeTime);

                yield return null;
            }
        }
    }
}