using System;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace ThanhDV.AudioConductor.FMOD
{
    public interface IAudioConductor : IDisposable
    {
        void SetVolume(Bus bus, float volume);
        float GetVolume(Bus bus);

        void PlayOneShot(EventReference sfxReference);
        void PlayOneShot(string sfxPath);
        void PlayOneShot(EventReference sfxReference, Vector3 position);
        void PlayOneShot(string sfxPath, Vector3 position);

        void PlayBGM(EventReference bgmReference, float fadeDuration = 1.0f);
        void PlayBGM(string bgmPath, float fadeDuration = 1.0f);
        void StopBGM(float fadeDuration = 1.0f);

        EventInstance PlayLoop(
            string id,
            string loopPath,
            GameObject attachedObject = null,
            Rigidbody attachedRigidbody = null);

        EventInstance PlayLoop(
            string id,
            EventReference loopReference,
            GameObject attachedObject = null,
            Rigidbody attachedRigidbody = null);

        void PauseLoop(string id);
        void ResumeLoop(string id);
        void StopLoop(
            string id,
            global::FMOD.Studio.STOP_MODE stopMode = global::FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        bool TryGetEventInstance(string id, out EventInstance instance);
    }
}
