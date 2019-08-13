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

    public static class AudioFade
    {
        public static IEnumerator FadeIn(AudioSource audioSource, float fadeTime, float initialVolume = 0f,
            float maxVolume = 1f)
        {
            audioSource.volume = initialVolume;

            while (audioSource.volume < maxVolume)
            {
                Mathf.Clamp01(audioSource.volume += 1f * Time.deltaTime * fadeTime);

                yield return null;
            }
        }

        public static IEnumerator FadeOut(AudioSource audioSource, float fadeTime, float initialVolume = 1f,
            float minimumVolume = 0f)
        {
            audioSource.volume = initialVolume;

            while (audioSource.volume > minimumVolume)
            {
                Mathf.Clamp01(audioSource.volume -= 1f * Time.deltaTime * fadeTime);
                yield return null;
            }
        }
    }
}