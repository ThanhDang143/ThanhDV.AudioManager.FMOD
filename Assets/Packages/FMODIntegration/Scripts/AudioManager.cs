using System;
using System.Collections.Generic;
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

                InitializeAudioBusses();
#if UNITY_EDITOR
                InitializeDebug();
#endif
                return;
            }

            Destroy(gameObject);
        }
        #endregion

        private EventInstance bgmInstance;
        private CancellationTokenSource bgmOperationCTS;
        private readonly Dictionary<string, EventInstance> loopingInstances = new();

        private Dictionary<AudioType, Bus> audioBusses;

#if UNITY_EDITOR

        [SerializeField, Range(0f, 1f)] private float masterVolume;
        [SerializeField, Range(0f, 1f)] private float bgmVolume;
        [SerializeField, Range(0f, 1f)] private float sfxVolume;

        private void InitializeDebug()
        {
            masterVolume = GetVolume(AudioType.MASTER);
            bgmVolume = GetVolume(AudioType.BGM);
            sfxVolume = GetVolume(AudioType.SFX);
        }

        private void OnValidate()
        {
            SetVolume(AudioType.MASTER, masterVolume);
            SetVolume(AudioType.BGM, bgmVolume);
            SetVolume(AudioType.SFX, sfxVolume);
        }

#endif

        #region Audio volume

        private void InitializeAudioBusses()
        {
            audioBusses = new();

            InitializeAudioBus(AudioType.MASTER, "bus:/");
            InitializeAudioBus(AudioType.BGM, "bus:/BGM");
            InitializeAudioBus(AudioType.SFX, "bus:/SFX");
        }

        private void InitializeAudioBus(AudioType type, string busPath)
        {
            try
            {
                Bus masterBus = RuntimeManager.GetBus(busPath);
                audioBusses.TryAdd(type, masterBus);
            }
            catch (BusNotFoundException)
            {
                Debug.Log($"<color=red>[FMODIntegration] Bus not found: '{busPath}'. Please check your FMOD Studio project!!!</color>");
            }
        }

        public void SetVolume(AudioType type, float volume)
        {
            if (!audioBusses.TryGetValue(type, out Bus bus))
            {
                Debug.Log($"<color=red>[FMODIntegration] Bus not found: '{type.ToString()}'. Please check your FMOD Studio project or Initialize first!!!</color>");
                return;
            }

            volume = Mathf.Clamp01(volume);
            bus.setVolume(volume);
        }

        public float GetVolume(AudioType type)
        {
            if (!audioBusses.TryGetValue(type, out Bus bus))
            {
                Debug.Log($"<color=red>[FMODIntegration] Bus not found: '{type.ToString()}'. Please check your FMOD Studio project or Initialize first!!!</color>");
                return -1f;
            }

            bus.getVolume(out float volume);
            return volume;
        }

        #endregion

        #region One-Shot
        /// <summary>
        /// Plays a one-shot sound.
        /// </summary>
        /// <param name="sfxReference">The FMOD Event Reference for the SFX.</param>
        public void PlayOneShot(EventReference sfxReference)
        {
            RuntimeManager.PlayOneShot(sfxReference);
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
            RuntimeManager.PlayOneShot(sfxReference, position);
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
            bgmOperationCTS?.Cancel();
            bgmOperationCTS = new CancellationTokenSource();

            _ = PerformBgmTransitionAsync(bgmReference, fadeDuration, delay, bgmOperationCTS.Token);
        }

        /// <summary>
        /// Plays a new BGM with a configurable transition.
        /// </summary>
        /// <param name="bgmPath">The Path of FMOD Event Reference for the new BGM.</param>
        /// <param name="fadeDuration">The duration of the fade for both outgoing and incoming tracks.</param>
        /// <param name="delay">The time to wait after the old track starts fading out before the new track starts fading in. 0 = Crossfade, fadeDuration = Sequential Fade.</param>
        public void PlayBGM(string bgmPath, float fadeDuration = 1.0f, float delay = 0f)
        {
            bgmOperationCTS?.Cancel();
            bgmOperationCTS = new CancellationTokenSource();

            _ = PerformBgmTransitionAsync(bgmPath, fadeDuration, delay, bgmOperationCTS.Token);
        }

        /// <summary>
        /// Stops the current BGM with a fade-out effect.
        /// </summary>
        /// <param name="fadeDuration">The duration of the fade for both outgoing tracks.</param>
        /// <param name="delay">The time to wait to start fading out.</param>
        public void StopBGM(float fadeDuration = 1.0f, float delay = 0)
        {
            bgmOperationCTS?.Cancel();
            bgmOperationCTS = new CancellationTokenSource();

            PlayBGM(null, fadeDuration, delay);
        }

        /// <summary>
        /// Handles transitioning the current BGM to a new track: fades out the current instance.
        /// </summary>
        /// <param name="newBgmPath">The FMOD event path for the new BGM. If null or empty, the current BGM is simply stopped.</param>
        /// <param name="duration">The fade duration (in seconds) applied to both the fade-out of the old instance and the fade-in of the new one.</param>
        /// <param name="delay">Optional delay (in seconds) after starting the old fade-out before starting and fading in the new instance. 0 = immediate / crossfade.</param>
        /// <param name="token">A cancellation token to abort the transition early; when cancelled, any started instance is stopped and released.</param>
        /// <returns>A Task representing the asynchronous transition operation.</returns>
        private async Task PerformBgmTransitionAsync(string newBgmPath, float duration, float delay, CancellationToken token)
        {
            EventInstance oldInstance = bgmInstance;
            if (oldInstance.isValid())
            {
                _ = FadeOutAndRelease(oldInstance, duration, token);
            }

            if (string.IsNullOrEmpty(newBgmPath))
            {
                bgmInstance = new EventInstance();
                return;
            }

            try
            {
                if (delay > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay), token);
                }

                if (token.IsCancellationRequested) return;

                EventInstance newInstance = RuntimeManager.CreateInstance(newBgmPath);
                newInstance.start();
                bgmInstance = newInstance;

                await FadeInstance(newInstance, 0f, 1.0f, duration, token);
            }
            catch (TaskCanceledException)
            {
                Debug.Log("<color=red>[FMODIntegration] BGM fade-in was cancelled!!!</color>");
                if (bgmInstance.isValid())
                {
                    bgmInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                    bgmInstance.release();
                }
            }
        }

        /// <summary>
        /// Handles transitioning the current BGM to a new track: fades out the current instance.
        /// </summary>
        /// <param name="newBgmRef">The FMOD event reference for the new BGM. If null or empty, the current BGM is simply stopped.</param>
        /// <param name="duration">The fade duration (in seconds) applied to both the fade-out of the old instance and the fade-in of the new one.</param>
        /// <param name="delay">Optional delay (in seconds) after starting the old fade-out before starting and fading in the new instance. 0 = immediate / crossfade.</param>
        /// <param name="token">A cancellation token to abort the transition early; when cancelled, any started instance is stopped and released.</param>
        /// <returns>A Task representing the asynchronous transition operation.</returns>
        private async Task PerformBgmTransitionAsync(EventReference newBgmRef, float duration, float delay, CancellationToken token)
        {
            EventInstance oldInstance = bgmInstance;
            if (oldInstance.isValid())
            {
                _ = FadeOutAndRelease(oldInstance, duration, token);
            }

            if (newBgmRef.IsNull)
            {
                bgmInstance = new EventInstance();
                return;
            }

            try
            {
                if (delay > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay), token);
                }

                if (token.IsCancellationRequested) return;

                EventInstance newInstance = RuntimeManager.CreateInstance(newBgmRef);
                newInstance.start();
                bgmInstance = newInstance;

                await FadeInstance(newInstance, 0f, 1.0f, duration, token);
            }
            catch (TaskCanceledException)
            {
                Debug.Log("<color=red>[FMODIntegration] BGM fade-in was cancelled!!!</color>");
                if (bgmInstance.isValid())
                {
                    bgmInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                    bgmInstance.release();
                }
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

        #region Loop Sound

        /// <summary>
        /// Starts a looping sound and keeps track of it using a unique ID.
        /// </summary>
        /// <param name="id">A unique string to identify this sound instance (e.g., "playerFootsteps", "faucet_1").</param>
        /// <param name="loopPath">The Path of FMOD Event Reference for the looping sound.</param>
        /// <param name="attachedObject">Optional: The GameObject to attach the sound to for 3D positioning.</param>
        public void PlayLoop(string id, string loopPath, GameObject attachedObject = null)
        {
            if (loopingInstances.ContainsKey(id)) return;

            EventInstance loopInstance = RuntimeManager.CreateInstance(loopPath);
            if (attachedObject != null)
            {
                RuntimeManager.AttachInstanceToGameObject(loopInstance, attachedObject.transform, attachedObject.GetComponent<Rigidbody>());
            }

            loopInstance.start();
            loopingInstances.Add(id, loopInstance);
        }

        /// <summary>
        /// Starts a looping sound and keeps track of it using a unique ID.
        /// </summary>
        /// <param name="id">A unique string to identify this sound instance (e.g., "playerFootsteps", "faucet_1").</param>
        /// <param name="loopReference">The FMOD Event Reference for the looping sound.</param>
        /// <param name="attachedObject">Optional: The GameObject to attach the sound to for 3D positioning.</param>
        public void PlayLoop(string id, EventReference loopReference, GameObject attachedObject = null)
        {
            if (loopingInstances.ContainsKey(id)) return;

            EventInstance loopInstance = RuntimeManager.CreateInstance(loopReference);
            if (attachedObject != null)
            {
                RuntimeManager.AttachInstanceToGameObject(loopInstance, attachedObject.transform, attachedObject.GetComponent<Rigidbody>());
            }

            loopInstance.start();
            loopingInstances.Add(id, loopInstance);
        }

        /// <summary>
        /// Pauses a specific looping sound.
        /// </summary>
        /// <param name="id">The unique ID of the sound to pause.</param>
        public void PauseLoop(string id)
        {
            if (loopingInstances.TryGetValue(id, out EventInstance instance))
            {
                instance.setPaused(true);
            }
        }

        /// <summary>
        /// Resumes a specific looping sound.
        /// </summary>
        /// <param name="id">The unique ID of the sound to resume.</param>
        public void ResumeLoop(string id)
        {
            if (loopingInstances.TryGetValue(id, out EventInstance instance))
            {
                instance.setPaused(false);
            }
        }


        /// <summary>
        /// Stops a looping sound identified by its unique ID.
        /// </summary>
        /// <param name = "id" > The unique ID of the sound to stop.</param>
        /// <param name = "stopMode" > How to stop the sound(e.g., allow fade out or immediate).</param>
        public void StopLoop(string id, FMOD.Studio.STOP_MODE stopMode = FMOD.Studio.STOP_MODE.ALLOWFADEOUT)
        {
            if (loopingInstances.TryGetValue(id, out EventInstance instance))
            {
                instance.stop(stopMode);
                instance.release();
                loopingInstances.Remove(id);
            }
            else
            {
                Debug.Log($"<color=red>[FMODIntegration] Could not find looping sound with ID '{id}' to stop!!!</color>");
            }
        }

        #endregion

        #region Cleanup
        private void OnDestroy()
        {
            // Cancel any ongoing BGM operations
            bgmOperationCTS?.Cancel();

            // Clean up the main BGM instance
            if (bgmInstance.isValid())
            {
                bgmInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                bgmInstance.release();
            }

            // Clean up all looping sounds
            foreach (var ins in loopingInstances)
            {
                ins.Value.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                ins.Value.release();
            }
            loopingInstances.Clear();
        }
        #endregion
    }

    public enum AudioType
    {
        MASTER,
        BGM,
        SFX
    }
}
