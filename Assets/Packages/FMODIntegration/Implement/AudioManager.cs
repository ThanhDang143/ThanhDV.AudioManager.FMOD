using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace ThanhDV.FMODIntegration
{
    public class AudioManager : MonoBehaviour
    {
        #region Singleton
        private static AudioManager _instance;
        private static readonly object _lock = new();

        public static AudioManager Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = FindFirstObjectByType(typeof(AudioManager)) as AudioManager;

                        if (_instance == null)
                        {
                            _instance = new GameObject("AudioManager").AddComponent<AudioManager>();

                            Debug.Log($"<color=yellow>{_instance.GetType().Name} instance is null!!! Auto create new instance!!!</color>");
                        }
                        DontDestroyOnLoad(_instance);
                    }
                    return _instance;
                }
            }
        }

        public static bool IsExist => _instance != null;

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this as AudioManager;
                DontDestroyOnLoad(_instance);
                return;
            }

            Destroy(gameObject);
        }
        #endregion

        private EventInstance bgmInstance;
        private CancellationTokenSource bgmFadeCTS;

        #region One-Shot
        /// <summary>
        /// Plays a one-shot sound.
        /// </summary>
        /// <param name="sfxReference">The FMOD Event Reference for the SFX.</param>
        public void PlayOneShot(EventReference sfxReference)
        {
            PlayOneShot(sfxReference.Path);
        }

        /// <summary>
        /// Plays a one-shot sound.
        /// </summary>
        /// <param name="sfxPath">The Path of FMOD Event Reference for the SFX.</param>
        public void PlayOneShot(string sfxPath)
        {
            RuntimeManager.PlayOneShot(sfxPath);
        }

        /// <summary>
        /// Plays a one-shot sound at a specific 3D position.
        /// </summary>
        /// <param name="sfxReference">The FMOD Event Reference for the SFX.</param>
        /// <param name="position">The world position to play the sound at.</param>
        public void PlayOneShot(EventReference sfxReference, Vector3 position)
        {
            PlayOneShot(sfxReference.Path, position);
        }

        /// <summary>
        /// Plays a one-shot sound at a specific 3D position.
        /// </summary>
        /// <param name="sfxPath">The Path of FMOD Event Reference for the SFX.</param>
        /// <param name="position">The world position to play the sound at.</param>
        public void PlayOneShot(string sfxPath, Vector3 position)
        {
            RuntimeManager.PlayOneShot(sfxPath, position);
        }
        #endregion

        #region BGM
        /// <summary>
        /// Plays a new BGM with a configurable transition.
        /// </summary>
        /// <param name="bgmReference">The FMOD Event Reference for the new BGM.</param>
        /// <param name="fadeDuration">The duration of the fade for both outgoing and incoming tracks.</param>
        /// <param name="delay">The time to wait after the old track starts fading out before the new track starts fading in. 0 = Crossfade, fadeDuration = Sequential Fade.</param>
        public void PlayBGM(EventReference bgmReference, float fadeDuration = 1.0f, float delay = 0f)
        {
            PlayBGM(bgmReference.Path, fadeDuration, delay);
        }

        /// <summary>
        /// Plays a new BGM with a configurable transition.
        /// </summary>
        /// <param name="bgmPath">The Path of FMOD Event Reference for the new BGM.</param>
        /// <param name="fadeDuration">The duration of the fade for both outgoing and incoming tracks.</param>
        /// <param name="delay">The time to wait after the old track starts fading out before the new track starts fading in. 0 = Crossfade, fadeDuration = Sequential Fade.</param>
        public async void PlayBGM(string bgmPath, float fadeDuration = 1.0f, float delay = 0f)
        {
            // Cancel the previous fade task if it's running to start the new one.
            bgmFadeCTS?.Cancel();
            bgmFadeCTS = new CancellationTokenSource();
            CancellationToken token = bgmFadeCTS.Token;

            EventInstance oldInstance = bgmInstance;

            if (oldInstance.isValid())
            {
                _ = FadeOutAndRelease(oldInstance, fadeDuration, token);
            }

            try
            {
                if (delay > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay), token);
                }

                EventInstance newInstance = RuntimeManager.CreateInstance(bgmPath);
                newInstance.start();
                bgmInstance = newInstance;

                await FadeInstance(newInstance, 0f, 1.0f, fadeDuration, token);
            }
            catch (TaskCanceledException)
            {
                Debug.Log("<color=red>[FMODIntegration] BGM fade task was cancelled!!!</color>");
                if (bgmInstance.isValid())
                {
                    bgmInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                    bgmInstance.release();
                }
            }
        }

        /// <summary>
        /// Stops the current BGM with a fade-out effect.
        /// </summary>
        /// <param name="fadeDuration">The duration of the fade for both outgoing tracks.</param>
        public async void StopBGM(float fadeDuration = 1.0f)
        {
            bgmFadeCTS?.Cancel();
            bgmFadeCTS = new CancellationTokenSource();
            CancellationToken token = bgmFadeCTS.Token;

            EventInstance oldInstance = bgmInstance;
            bgmInstance = new EventInstance();

            if (oldInstance.isValid())
            {
                await FadeOutAndRelease(oldInstance, fadeDuration, token);
            }
        }

        /// <summary>
        /// Asynchronous helper function to handle the volume fading of an EventInstance.
        /// </summary>
        /// <param name="instance">The EventInstance to fade.</param>
        /// <param name="startVolume">The starting volume.</param>
        /// <param name="endVolume">The target volume.</param>
        /// <param name="duration">The duration of the fade.</param>
        /// <param name="token">The cancellation token to stop the task.</param>
        /// <returns>A Task representing the asynchronous operation.</returns>
        private async Task FadeInstance(EventInstance instance, float startVolume, float endVolume, float duration, CancellationToken token)
        {
            float time = 0;
            while (time < duration)
            {
                token.ThrowIfCancellationRequested();
                time += Time.deltaTime;
                float volume = Mathf.Lerp(startVolume, endVolume, time / duration);
                instance.setVolume(volume);
                await Task.Yield();
            }
            instance.setVolume(endVolume);
        }

        /// <summary>
        /// A dedicated, self-contained task to fade out and then release an instance.
        /// </summary>
        private async Task FadeOutAndRelease(EventInstance instance, float duration, CancellationToken token)
        {
            try
            {
                instance.getVolume(out float startVolume);
                await FadeInstance(instance, startVolume, 0f, duration, token);
            }
            catch (TaskCanceledException)
            {
                Debug.Log("<color=red>[FMODIntegration] FadeOutAndRelease task was cancelled!!!</color>");
            }
            finally
            {
                instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                instance.release();
            }
        }
        #endregion
    }
}
