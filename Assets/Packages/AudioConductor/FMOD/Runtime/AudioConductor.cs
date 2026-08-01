using UnityEngine;

namespace ThanhDV.AudioConductor.FMOD
{
    public static class AudioConductor
    {
        private static readonly object _lock = new();
        private static FMODConductor _instance;
        private static AudioConductorLifecycle _lifecycle;

        public static IAudioConductor Instance
        {
            get
            {
                lock (_lock)
                {
                    return _instance ??= CreateInstance();
                }
            }
        }

        private static FMODConductor CreateInstance()
        {
            EnsureLifecycle();
            return new FMODConductor();
        }

        private static void EnsureLifecycle()
        {
            if (_lifecycle != null) return;

            var lifecycleObject = new GameObject(nameof(AudioConductor));
            Object.DontDestroyOnLoad(lifecycleObject);
            _lifecycle = lifecycleObject.AddComponent<AudioConductorLifecycle>();
        }

        public static bool IsExist => _instance != null;

        public static void Dispose()
        {
            lock (_lock)
            {
                _instance?.Dispose();
                _instance = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Dispose();
            _lifecycle = null;
        }
    }
}
