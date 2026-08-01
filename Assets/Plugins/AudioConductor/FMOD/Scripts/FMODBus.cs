using FMOD.Studio;
using FMODUnity;
using UnityEngine;

namespace ThanhDV.AudioConductor.FMOD
{
    public static class FMODBus
    {
        private static Bus? _cachedSFX_Character;
        public static Bus SFX_Character => GetBus("bus:/SFX/Character", ref _cachedSFX_Character);

        private static Bus? _cachedSFX_Ambience;
        public static Bus SFX_Ambience => GetBus("bus:/SFX/Ambience", ref _cachedSFX_Ambience);

        private static Bus? _cachedSFX_Explosions;
        public static Bus SFX_Explosions => GetBus("bus:/SFX/Explosions", ref _cachedSFX_Explosions);

        private static Bus? _cachedVO;
        public static Bus VO => GetBus("bus:/VO", ref _cachedVO);

        private static Bus? _cachedSFX_Vehicles;
        public static Bus SFX_Vehicles => GetBus("bus:/SFX/Vehicles", ref _cachedSFX_Vehicles);

        private static Bus? _cachedSFX_Weapons;
        public static Bus SFX_Weapons => GetBus("bus:/SFX/Weapons", ref _cachedSFX_Weapons);

        private static Bus? _cachedSFX;
        public static Bus SFX => GetBus("bus:/SFX", ref _cachedSFX);

        private static Bus? _cachedMusic;
        public static Bus Music => GetBus("bus:/Music", ref _cachedMusic);

        private static Bus? _cachedSFX_Objects;
        public static Bus SFX_Objects => GetBus("bus:/SFX/Objects", ref _cachedSFX_Objects);

        private static Bus? _cachedUI;
        public static Bus UI => GetBus("bus:/UI", ref _cachedUI);

        private static Bus? _cachedSFX_Reverb;
        public static Bus SFX_Reverb => GetBus("bus:/SFX/Reverb", ref _cachedSFX_Reverb);

        private static Bus? _cachedMaster;
        public static Bus Master => GetBus("bus:/", ref _cachedMaster);

        public static readonly string[] Keys = { "SFX_Character", "SFX_Ambience", "SFX_Explosions", "VO", "SFX_Vehicles", "SFX_Weapons", "SFX", "Music", "SFX_Objects", "UI", "SFX_Reverb", "Master" };

        public static Bus GetByKey(string key)
        {
            switch (key)
            {
                case "SFX_Character": return SFX_Character;
                case "SFX_Ambience": return SFX_Ambience;
                case "SFX_Explosions": return SFX_Explosions;
                case "VO": return VO;
                case "SFX_Vehicles": return SFX_Vehicles;
                case "SFX_Weapons": return SFX_Weapons;
                case "SFX": return SFX;
                case "Music": return Music;
                case "SFX_Objects": return SFX_Objects;
                case "UI": return UI;
                case "SFX_Reverb": return SFX_Reverb;
                case "Master": return Master;
                default:
                    Debug.LogWarning($"[AudioConductor - FMOD] Bus key '{key}' not found in FMODBus.");
                    return default;
            }
        }

        private static Bus GetBus(string path, ref Bus? cache)
        {
            if (cache.HasValue && cache.Value.isValid()) return cache.Value;
            try
            {
                Bus result = RuntimeManager.GetBus(path);
                if (result.isValid()) cache = result;
                return result;
            }
            catch (BusNotFoundException)
            {
                Debug.LogWarning($"[AudioConductor - FMOD] Bus not found: '{path}'. Please check your FMOD Studio project!!!");
                return default;
            }
        }
    }
}
