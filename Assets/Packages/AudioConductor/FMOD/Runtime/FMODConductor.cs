using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace ThanhDV.AudioConductor.FMOD
{
    public sealed class FMODConductor : IAudioConductor
    {
        public FMODConductor()
        {
#if UNITY_EDITOR
            CheckSupportedImportType();
#endif
        }

        private EventInstance _bgmInstance;
        private CancellationTokenSource _bgmOperationCTS;
        private readonly Dictionary<string, EventInstance> _createdInstances = new();

        private static bool CheckResult(global::FMOD.RESULT result, string operation)
        {
            if (result == global::FMOD.RESULT.OK) return true;

            string errorDescription = global::FMOD.Error.String(result);
            Debug.LogError($"[AudioConductor - FMOD] {operation} failed: {result} - {errorDescription}");
            return false;
        }

        private static void ReleaseInstance(EventInstance instance, string context)
        {
            if (!instance.isValid()) return;

            CheckResult(instance.release(), $"Release {context}");
        }

        private static void StopAndReleaseInstance(
            EventInstance instance,
            global::FMOD.Studio.STOP_MODE stopMode,
            string context)
        {
            if (!instance.isValid()) return;

            CheckResult(instance.stop(stopMode), $"Stop {context}");
            CheckResult(instance.release(), $"Release {context}");
        }

        private CancellationToken BeginBgmOperation()
        {
            _bgmOperationCTS?.Cancel();
            _bgmOperationCTS?.Dispose();

            _bgmOperationCTS = new CancellationTokenSource();
            return _bgmOperationCTS.Token;
        }

#if UNITY_EDITOR
        private static void CheckSupportedImportType()
        {
            Settings settings = Settings.Instance;
            if (settings != null && settings.ImportType == ImportType.AssetBundle)
            {
                Debug.LogError("[AudioConductor - FMOD] FMOD Import Type 'Asset Bundle' is not supported. Banks won't auto-load; use 'Streaming Assets'.");
            }
        }
#endif

        #region Audio volume
        public void SetVolume(Bus bus, float volume)
        {
            if (!bus.isValid())
            {
                Debug.LogError($"[AudioConductor - FMOD] Bus is invalid!!!");
                return;
            }

            volume = Mathf.Clamp01(volume);
            CheckResult(bus.setVolume(volume), "Set bus volume");
        }

        public float GetVolume(Bus bus)
        {
            if (!bus.isValid())
            {
                Debug.LogError($"[AudioConductor - FMOD] Bus is invalid!!!");
                return -1;
            }

            if (!CheckResult(bus.getVolume(out float volume), "Get bus volume")) return -1;

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
            if (sfxReference.IsNull()) return;

            PlayOneShot(sfxReference, Vector3.zero);
        }

        /// <summary>
        /// Plays a one-shot sound.
        /// </summary>
        /// <param name="sfxPath">The Path of FMOD Event Reference for the SFX.</param>
        public void PlayOneShot(string sfxPath)
        {
            if (sfxPath.IsEventReferenceNull()) return;

            PlayOneShot(sfxPath, Vector3.zero);
        }

        /// <summary>
        /// Plays a one-shot sound at a specific 3D position.
        /// </summary>
        /// <param name="sfxReference">The FMOD Event Reference for the SFX.</param>
        /// <param name="position">The world position to play the sound at.</param>
        public void PlayOneShot(EventReference sfxReference, Vector3 position)
        {
            if (sfxReference.IsNull()) return;

            try
            {
                PlayOneShotInstance(RuntimeManager.CreateInstance(sfxReference), position, sfxReference.ToString());
            }
            catch (EventNotFoundException)
            {
                Debug.LogWarning($"[AudioConductor - FMOD] Event not found: {sfxReference}");
            }
        }

        /// <summary>
        /// Plays a one-shot sound at a specific 3D position.
        /// </summary>
        /// <param name="sfxPath">The Path of FMOD Event Reference for the SFX.</param>
        /// <param name="position">The world position to play the sound at.</param>
        public void PlayOneShot(string sfxPath, Vector3 position)
        {
            if (sfxPath.IsEventReferenceNull()) return;

            try
            {
                PlayOneShotInstance(RuntimeManager.CreateInstance(sfxPath), position, sfxPath);
            }
            catch (EventNotFoundException)
            {
                Debug.LogWarning($"[AudioConductor - FMOD] Event not found: {sfxPath}");
            }
        }

        private static void PlayOneShotInstance(EventInstance instance, Vector3 position, string eventName)
        {
            if (!instance.isValid())
            {
                Debug.LogError($"[AudioConductor - FMOD] Could not create one-shot event '{eventName}'.");
                return;
            }

            if (!CheckResult(
                    instance.set3DAttributes(RuntimeUtils.To3DAttributes(position)),
                    $"Set 3D attributes for one-shot event '{eventName}'"))
            {
                ReleaseInstance(instance, $"one-shot event '{eventName}'");
                return;
            }

            if (!CheckResult(instance.start(), $"Start one-shot event '{eventName}'"))
            {
                ReleaseInstance(instance, $"one-shot event '{eventName}'");
                return;
            }

            ReleaseInstance(instance, $"one-shot event '{eventName}'");
        }
        #endregion

        #region BGM
        /// <summary>
        /// Plays a new BGM with a configurable transition.
        /// </summary>
        /// <param name="bgmReference">The FMOD Event Reference for the new BGM.</param>
        /// <param name="fadeDuration">The duration of the fade for both outgoing and incoming tracks.</param>
        public void PlayBGM(EventReference bgmReference, float fadeDuration = 1.0f)
        {
            CancellationToken token = BeginBgmOperation();
            _ = PerformBgmTransitionAsync(bgmReference, fadeDuration, token);
        }

        /// <summary>
        /// Plays a new BGM with a configurable transition.
        /// </summary>
        /// <param name="bgmPath">The Path of FMOD Event Reference for the new BGM.</param>
        /// <param name="fadeDuration">The duration of the fade for both outgoing and incoming tracks.</param>
        public void PlayBGM(string bgmPath, float fadeDuration = 1.0f)
        {
            CancellationToken token = BeginBgmOperation();
            _ = PerformBgmTransitionAsync(bgmPath, fadeDuration, token);
        }

        /// <summary>
        /// Stops the current BGM with a fade-out effect.
        /// </summary>
        /// <param name="fadeDuration">The duration of the fade for both outgoing tracks.</param>
        public void StopBGM(float fadeDuration = 1.0f)
        {
            CancellationToken token = BeginBgmOperation();

            EventInstance oldInstance = _bgmInstance;
            _bgmInstance = default;

            if (oldInstance.isValid())
            {
                _ = FadeOutAndRelease(oldInstance, fadeDuration, token);
            }
        }

        /// <summary>
        /// Handles transitioning the current BGM to a new track: fades out the current instance.
        /// </summary>
        /// <param name="newBgmPath">The FMOD event path for the new BGM. If null or empty, the current BGM is simply stopped.</param>
        /// <param name="duration">The fade duration (in seconds) applied to both the fade-out of the old instance and the fade-in of the new one.</param>
        /// <param name="token">A cancellation token used to abort this transition when a newer BGM operation or Dispose takes ownership of cleanup.</param>
        /// <returns>A Task representing the asynchronous transition operation.</returns>
        private async Task PerformBgmTransitionAsync(string newBgmPath, float duration, CancellationToken token)
        {
            EventInstance oldInstance = _bgmInstance;
            if (oldInstance.isValid())
            {
                _ = FadeOutAndRelease(oldInstance, duration, token);
            }

            if (string.IsNullOrEmpty(newBgmPath))
            {
                _bgmInstance = new EventInstance();
                return;
            }

            try
            {
                if (token.IsCancellationRequested) return;

                EventInstance newInstance = RuntimeManager.CreateInstance(newBgmPath);
                if (!newInstance.isValid())
                {
                    Debug.LogError($"[AudioConductor - FMOD] Could not create BGM event '{newBgmPath}'.");
                    if (!token.IsCancellationRequested)
                    {
                        _bgmInstance = default;
                    }
                    return;
                }

                if (!CheckResult(newInstance.start(), $"Start BGM event '{newBgmPath}'"))
                {
                    ReleaseInstance(newInstance, $"BGM event '{newBgmPath}'");
                    if (!token.IsCancellationRequested)
                    {
                        _bgmInstance = default;
                    }
                    return;
                }

                _bgmInstance = newInstance;

                if (!await FadeInstance(newInstance, 0f, 1.0f, duration, token) &&
                    !token.IsCancellationRequested)
                {
                    if (_bgmInstance.handle == newInstance.handle)
                    {
                        _bgmInstance = default;
                    }

                    StopAndReleaseInstance(newInstance, global::FMOD.Studio.STOP_MODE.IMMEDIATE, $"BGM event '{newBgmPath}'");
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // A newer BGM operation or Dispose owns cleanup. Do not touch the shared instance here.
            }
            catch (Exception exception)
            {
                if (!token.IsCancellationRequested)
                {
                    _bgmInstance = default;
                }

                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Handles transitioning the current BGM to a new track: fades out the current instance.
        /// </summary>
        /// <param name="newBgmRef">The FMOD event reference for the new BGM. If null or empty, the current BGM is simply stopped.</param>
        /// <param name="duration">The fade duration (in seconds) applied to both the fade-out of the old instance and the fade-in of the new one.</param>
        /// <param name="token">A cancellation token used to abort this transition when a newer BGM operation or Dispose takes ownership of cleanup.</param>
        /// <returns>A Task representing the asynchronous transition operation.</returns>
        private async Task PerformBgmTransitionAsync(EventReference newBgmRef, float duration, CancellationToken token)
        {
            EventInstance oldInstance = _bgmInstance;
            if (oldInstance.isValid())
            {
                _ = FadeOutAndRelease(oldInstance, duration, token);
            }

            if (newBgmRef.IsNull)
            {
                _bgmInstance = new EventInstance();
                return;
            }

            try
            {
                if (token.IsCancellationRequested) return;

                EventInstance newInstance = RuntimeManager.CreateInstance(newBgmRef);
                if (!newInstance.isValid())
                {
                    Debug.LogError($"[AudioConductor - FMOD] Could not create BGM event '{newBgmRef}'.");
                    if (!token.IsCancellationRequested)
                    {
                        _bgmInstance = default;
                    }
                    return;
                }

                if (!CheckResult(newInstance.start(), $"Start BGM event '{newBgmRef}'"))
                {
                    ReleaseInstance(newInstance, $"BGM event '{newBgmRef}'");
                    if (!token.IsCancellationRequested)
                    {
                        _bgmInstance = default;
                    }
                    return;
                }

                _bgmInstance = newInstance;

                if (!await FadeInstance(newInstance, 0f, 1.0f, duration, token) &&
                    !token.IsCancellationRequested)
                {
                    if (_bgmInstance.handle == newInstance.handle)
                    {
                        _bgmInstance = default;
                    }

                    StopAndReleaseInstance(newInstance, global::FMOD.Studio.STOP_MODE.IMMEDIATE, $"BGM event '{newBgmRef}'");
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // A newer BGM operation or Dispose owns cleanup. Do not touch the shared instance here.
            }
            catch (Exception exception)
            {
                if (!token.IsCancellationRequested)
                {
                    _bgmInstance = default;
                }

                Debug.LogException(exception);
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
        private async Task<bool> FadeInstance(EventInstance instance, float startVolume, float endVolume, float duration, CancellationToken token)
        {
            float time = 0;
            while (time < duration)
            {
                token.ThrowIfCancellationRequested();
                time += Time.unscaledDeltaTime;
                float volume = Mathf.Lerp(startVolume, endVolume, time / duration);
                if (!CheckResult(instance.setVolume(volume), "Fade BGM volume")) return false;

                await Task.Yield();
            }

            return CheckResult(instance.setVolume(endVolume), "Set final BGM volume");
        }

        /// <summary>
        /// A dedicated, self-contained task to fade out and then release an instance.
        /// </summary>
        private async Task FadeOutAndRelease(EventInstance instance, float duration, CancellationToken token)
        {
            try
            {
                if (CheckResult(instance.getVolume(out float startVolume), "Get BGM volume before fade-out"))
                {
                    await FadeInstance(instance, startVolume, 0f, duration, token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // Expected: a newer BGM operation or Dispose cancelled this fade.
            }
            finally
            {
                StopAndReleaseInstance(instance, global::FMOD.Studio.STOP_MODE.IMMEDIATE, "BGM event after fade-out");
            }
        }
        #endregion

        #region Loop Sound
        private bool TryGetActiveLoop(string id, out EventInstance instance)
        {
            if (!_createdInstances.TryGetValue(id, out instance)) return false;

            if (!instance.isValid())
            {
                _createdInstances.Remove(id);
                instance = default;
                return false;
            }

            if (!CheckResult(instance.getPlaybackState(out PLAYBACK_STATE state), $"Get playback state for loop '{id}'"))
            {
                RuntimeManager.DetachInstanceFromGameObject(instance);
                StopAndReleaseInstance(instance, global::FMOD.Studio.STOP_MODE.IMMEDIATE, $"loop '{id}' after playback-state failure");
                _createdInstances.Remove(id);
                instance = default;
                return false;
            }
            if (state != PLAYBACK_STATE.STOPPED) return true;

            RuntimeManager.DetachInstanceFromGameObject(instance);
            ReleaseInstance(instance, $"stopped loop '{id}'");
            _createdInstances.Remove(id);
            instance = default;
            return false;
        }

        /// <summary>
        /// Starts a looping sound and keeps track of it using a unique ID.
        /// </summary>
        /// <param name="id">A unique string to identify this sound instance (e.g., "playerFootsteps", "faucet_1").</param>
        /// <param name="loopPath">The Path of FMOD Event Reference for the looping sound.</param>
        /// <param name="attachedObject">Optional: The GameObject to attach the sound to for 3D positioning.</param>
        /// <param name="attachedRigidbody">Optional: The Rigidbody to attach the sound to for Doppler effect.</param>
        public EventInstance PlayLoop(string id, string loopPath, GameObject attachedObject = null, Rigidbody attachedRigidbody = null)
        {
            if (TryGetActiveLoop(id, out EventInstance existingInstance)) return existingInstance;

            EventInstance loopInstance = RuntimeManager.CreateInstance(loopPath);
            if (!loopInstance.isValid())
            {
                Debug.LogError($"[AudioConductor - FMOD] Could not create loop event '{loopPath}' for ID '{id}'.");
                return default;
            }
            if (attachedObject != null)
            {
                RuntimeManager.AttachInstanceToGameObject(loopInstance, attachedObject, attachedRigidbody);
            }

            if (!CheckResult(loopInstance.start(), $"Start loop event '{loopPath}' for ID '{id}'"))
            {
                if (attachedObject != null)
                {
                    RuntimeManager.DetachInstanceFromGameObject(loopInstance);
                }
                ReleaseInstance(loopInstance, $"loop event '{loopPath}' for ID '{id}'");
                return default;
            }
            _createdInstances.Add(id, loopInstance);

            return loopInstance;
        }

        /// <summary>
        /// Starts a looping sound and keeps track of it using a unique ID.
        /// </summary>
        /// <param name="id">A unique string to identify this sound instance (e.g., "playerFootsteps", "faucet_1").</param>
        /// <param name="loopReference">The FMOD Event Reference for the looping sound.</param>
        /// <param name="attachedObject">Optional: The GameObject to attach the sound to for 3D positioning.</param>
        /// <param name="attachedRigidbody">Optional: The Rigidbody to attach the sound to for Doppler effect.</param>
        public EventInstance PlayLoop(string id, EventReference loopReference, GameObject attachedObject = null, Rigidbody attachedRigidbody = null)
        {
            if (TryGetActiveLoop(id, out EventInstance existingInstance)) return existingInstance;

            EventInstance loopInstance = RuntimeManager.CreateInstance(loopReference);
            if (!loopInstance.isValid())
            {
                Debug.LogError($"[AudioConductor - FMOD] Could not create loop event '{loopReference}' for ID '{id}'.");
                return default;
            }
            if (attachedObject != null)
            {
                RuntimeManager.AttachInstanceToGameObject(loopInstance, attachedObject, attachedRigidbody);
            }

            if (!CheckResult(loopInstance.start(), $"Start loop event '{loopReference}' for ID '{id}'"))
            {
                if (attachedObject != null)
                {
                    RuntimeManager.DetachInstanceFromGameObject(loopInstance);
                }
                ReleaseInstance(loopInstance, $"loop event '{loopReference}' for ID '{id}'");
                return default;
            }
            _createdInstances.Add(id, loopInstance);

            return loopInstance;
        }

        /// <summary>
        /// Pauses a specific looping sound.
        /// </summary>
        /// <param name="id">The unique ID of the sound to pause.</param>
        public void PauseLoop(string id)
        {
            if (TryGetActiveLoop(id, out EventInstance instance))
            {
                CheckResult(instance.setPaused(true), $"Pause loop '{id}'");
            }
            else
            {
                Debug.LogError($"[AudioConductor - FMOD] Could not find looping sound with ID '{id}' to pause!!!");
            }
        }

        /// <summary>
        /// Resumes a specific looping sound.
        /// </summary>
        /// <param name="id">The unique ID of the sound to resume.</param>
        public void ResumeLoop(string id)
        {
            if (TryGetActiveLoop(id, out EventInstance instance))
            {
                CheckResult(instance.setPaused(false), $"Resume loop '{id}'");
            }
            else
            {
                Debug.LogError($"[AudioConductor - FMOD] Could not find looping sound with ID '{id}' to resume!!!");
            }
        }

        /// <summary>
        /// Stops a looping sound identified by its unique ID.
        /// </summary>
        /// <param name = "id" > The unique ID of the sound to stop.</param>
        /// <param name = "stopMode" > How to stop the sound(e.g., allow fade out or immediate).</param>
        public void StopLoop(string id, global::FMOD.Studio.STOP_MODE stopMode = global::FMOD.Studio.STOP_MODE.ALLOWFADEOUT)
        {
            if (_createdInstances.TryGetValue(id, out EventInstance instance))
            {
                RuntimeManager.DetachInstanceFromGameObject(instance);
                StopAndReleaseInstance(instance, stopMode, $"loop '{id}'");
                _createdInstances.Remove(id);
            }
            else
            {
                Debug.LogError($"[AudioConductor - FMOD] Could not find looping sound with ID '{id}' to stop!!!");
            }
        }

        #endregion

        #region Others
        /// <summary>
        /// Get EventInstance for setParameter
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool TryGetEventInstance(string id, out EventInstance instance)
        {
            if (!TryGetActiveLoop(id, out instance))
            {
                Debug.LogError($"[AudioConductor - FMOD] Could not find looping sound with ID '{id}'!!!");
                return false;
            }

            return true;
        }
        #endregion

        #region Cleanup
        public void Dispose()
        {
            // Cancel any ongoing BGM operations
            CancellationTokenSource bgmOperationCTS = _bgmOperationCTS;
            _bgmOperationCTS = null;
            bgmOperationCTS?.Cancel();
            bgmOperationCTS?.Dispose();

            // Clean up the main BGM instance
            EventInstance bgmInstance = _bgmInstance;
            _bgmInstance = default;
            if (bgmInstance.isValid())
            {
                StopAndReleaseInstance(bgmInstance, global::FMOD.Studio.STOP_MODE.IMMEDIATE, "BGM event during Dispose");
            }

            // Clean up all looping sounds
            foreach (var ins in _createdInstances)
            {
                RuntimeManager.DetachInstanceFromGameObject(ins.Value);
                StopAndReleaseInstance(ins.Value, global::FMOD.Studio.STOP_MODE.IMMEDIATE, $"loop '{ins.Key}' during Dispose");
            }
            _createdInstances.Clear();
        }
        #endregion
    }
}
