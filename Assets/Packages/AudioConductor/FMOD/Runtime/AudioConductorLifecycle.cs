using UnityEngine;

namespace ThanhDV.AudioConductor.FMOD
{
    internal sealed class AudioConductorLifecycle : MonoBehaviour
    {
        private void OnDestroy()
        {
            AudioConductor.Dispose();
        }
    }
}
