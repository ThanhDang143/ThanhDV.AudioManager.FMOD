using System;
using System.Collections.Generic;
using FMOD.Studio;
using FMODUnity;
using UnityEditor;
using UnityEngine;

namespace ThanhDV.AudioConductor.FMOD
{
    public class VolumeDebugger : EditorWindow
    {
        [SerializeField] private List<AudioVolume> _audioVolumes = new();
        private Vector2 _scrollPosition;
        private bool _initialized;
        private string _error;

        private static bool CheckResult(global::FMOD.RESULT result, string operation)
        {
            if (result == global::FMOD.RESULT.OK) return true;

            string errorDescription = global::FMOD.Error.String(result);
            Debug.LogError($"[AudioConductor - FMOD] {operation} failed: {result} - {errorDescription}");
            return false;
        }

        [MenuItem(Common.MENU_ITEM + "Volume Debugger", false, 4)]
        public static void ShowWindow()
        {
            VolumeDebugger window = GetWindow<VolumeDebugger>();
            window.titleContent = new GUIContent("Volume Debugger");
            window.minSize = new Vector2(400, 250);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            if (EditorApplication.isPlaying) InitializeDebug();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                InitializeDebug();
            else if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
                ResetDebug();

            Repaint();
        }

        private void OnInspectorUpdate()
        {
            if (!EditorApplication.isPlaying || !_initialized) return;

            GetVolumes();
            Repaint();
        }

        private void OnGUI()
        {
            EditorHelper.CreateHeader("AudioConductor - FMOD - Volume Debugger", "Created by ThanhDV");

            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect and change FMOD bus volumes.", MessageType.Info);
                return;
            }

            if (GUILayout.Button("Refresh")) InitializeDebug();

            if (!string.IsNullOrEmpty(_error))
            {
                EditorGUILayout.HelpBox(_error, MessageType.Error);
                return;
            }

            if (!_initialized) return;

            if (_audioVolumes.Count == 0)
            {
                EditorGUILayout.HelpBox("No FMOD buses were found in FMODReferences JSON.", MessageType.Info);
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            for (int i = 0; i < _audioVolumes.Count; i++)
            {
                AudioVolume audioVolume = _audioVolumes[i];
                EditorGUI.BeginChangeCheck();
                float volume = EditorGUILayout.Slider(
                    new GUIContent(audioVolume.Key, audioVolume.BusPath),
                    audioVolume.Volume,
                    0f,
                    1f);

                if (EditorGUI.EndChangeCheck())
                {
                    if (SetVolume(audioVolume, volume)) audioVolume.Volume = volume;
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void InitializeDebug()
        {
            ResetDebug();
            if (!EditorApplication.isPlaying) return;

            FMODReferencesLoadStatus status = FMODReferencesStore.Load(out FMODReferencesData data, out string error);
            if (status == FMODReferencesLoadStatus.Invalid)
            {
                _error = error;
                _initialized = true;
                return;
            }

            foreach (BusEntry entry in data.Buses)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.BusPath)) continue;

                var audioVolume = new AudioVolume(entry.Key, entry.BusPath);
                float volume = GetVolume(audioVolume);
                if (volume >= 0f) audioVolume.Volume = volume;
                _audioVolumes.Add(audioVolume);
            }

            _initialized = true;
        }

        private void ResetDebug()
        {
            _initialized = false;
            _error = null;
            _audioVolumes = new List<AudioVolume>();
        }

        private void GetVolumes()
        {
            for (int i = 0; i < _audioVolumes.Count; i++)
            {
                AudioVolume audioVolume = _audioVolumes[i];
                float volume = GetVolume(audioVolume);
                if (volume >= 0f) audioVolume.Volume = volume;
            }
        }

        private static bool SetVolume(AudioVolume audioVolume, float volume)
        {
            Bus bus = GetBus(audioVolume);
            if (!bus.isValid()) return false;

            return CheckResult(bus.setVolume(Mathf.Clamp01(volume)), $"Set volume for bus '{audioVolume.BusPath}'");
        }

        private static float GetVolume(AudioVolume audioVolume)
        {
            Bus bus = GetBus(audioVolume);
            if (!bus.isValid()) return -1f;

            global::FMOD.RESULT result = bus.getVolume(out float volume);
            if (result != global::FMOD.RESULT.OK)
            {
                if (!audioVolume.GetVolumeErrorLogged)
                {
                    CheckResult(result, $"Get volume for bus '{audioVolume.BusPath}'");
                }

                audioVolume.GetVolumeErrorLogged = true;
                return -1f;
            }

            audioVolume.GetVolumeErrorLogged = false;
            return volume;
        }

        private static Bus GetBus(AudioVolume audioVolume)
        {
            if (audioVolume.CachedBus.HasValue && audioVolume.CachedBus.Value.isValid()) return audioVolume.CachedBus.Value;

            try
            {
                Bus bus = RuntimeManager.GetBus(audioVolume.BusPath);
                if (bus.isValid()) audioVolume.CachedBus = bus;
                return bus;
            }
            catch (BusNotFoundException)
            {
                Debug.LogWarning($"[AudioConductor - FMOD] Bus not found: '{audioVolume.BusPath}'. Please check your FMOD Studio project!!!");
                return default;
            }
        }

        [Serializable]
        private class AudioVolume
        {
            public string Key;
            public string BusPath;
            [Range(0f, 1f)] public float Volume;
            [NonSerialized] public Bus? CachedBus;
            [NonSerialized] public bool GetVolumeErrorLogged;

            public AudioVolume(string key, string busPath)
            {
                Key = key;
                BusPath = busPath;
            }
        }
    }
}
