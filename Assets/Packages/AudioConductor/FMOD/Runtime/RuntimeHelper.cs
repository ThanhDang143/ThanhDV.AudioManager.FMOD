using FMODUnity;
using UnityEngine;

namespace ThanhDV.AudioConductor.FMOD
{
    public static class RuntimeHelper
    {
        public static bool IsNull(this EventReference eventReference)
        {
            if (eventReference.IsNull)
            {
                Debug.LogError("[AudioConductor - FMOD] Cannot play sound: The provided EventReference is null.");
                return true;
            }

            return false;
        }

        public static bool IsEventReferenceNull(this string eventReferencePath)
        {
            if (string.IsNullOrEmpty(eventReferencePath) || string.IsNullOrWhiteSpace(eventReferencePath))
            {
                Debug.LogError("[AudioConductor - FMOD] Cannot play sound: The provided eventReferencePath is null.");
                return true;
            }

            return false;
        }
    }
}
