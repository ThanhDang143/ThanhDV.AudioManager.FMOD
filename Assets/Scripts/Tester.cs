using FMOD.Studio;
using UnityEngine;

namespace ThanhDV.AudioConductor.FMOD
{
    public class Tester : MonoBehaviour
    {
        private const string LOOP_ID = "Loop";

        private void Start()
        {
            AudioConductor.Instance.PlayBGM(FMODEventReference.Music_Level_02);
        }

        private void OnGUI()
        {
            const float width = 280f;
            const float height = 46f;

            GUI.skin.button.fontSize = 18;

            GUILayout.BeginArea(new Rect(12, 12, width + 16, Screen.height - 24));

            if (GUILayout.Button("Play BGM", GUILayout.Width(width), GUILayout.Height(height)))
                AudioConductor.Instance.PlayBGM(FMODEventReference.Music_Level_02);

            if (GUILayout.Button("Stop BGM", GUILayout.Width(width), GUILayout.Height(height)))
                AudioConductor.Instance.StopBGM();

            if (GUILayout.Button("One-Shot", GUILayout.Width(width), GUILayout.Height(height)))
                AudioConductor.Instance.PlayOneShot(FMODEventReference.UI_Okay);

            if (GUILayout.Button("Play Loop", GUILayout.Width(width), GUILayout.Height(height)))
                AudioConductor.Instance.PlayLoop(LOOP_ID, FMODEventReference.Weapons_Machine_Gun, gameObject);

            if (GUILayout.Button("Stop Loop", GUILayout.Width(width), GUILayout.Height(height)))
                AudioConductor.Instance.StopLoop(LOOP_ID);

            if (GUILayout.Button("Loop Pitch x2", GUILayout.Width(width), GUILayout.Height(height)))
            {
                if (AudioConductor.Instance.TryGetEventInstance(LOOP_ID, out EventInstance instance))
                    instance.setPitch(2f);
            }

            if (GUILayout.Button("Loop Pitch x0.5", GUILayout.Width(width), GUILayout.Height(height)))
            {
                if (AudioConductor.Instance.TryGetEventInstance(LOOP_ID, out EventInstance instance))
                    instance.setPitch(0.5f);
            }

            if (GUILayout.Button("Dispose AudioConductor", GUILayout.Width(width), GUILayout.Height(height)))
                AudioConductor.Dispose();

            GUILayout.EndArea();
        }
    }
}
