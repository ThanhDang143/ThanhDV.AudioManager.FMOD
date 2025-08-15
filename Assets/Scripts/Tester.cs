using System.Collections;
using System.Collections.Generic;
using FMODUnity;
using UnityEngine;

namespace ThanhDV.FMODIntegration
{
    public class Tester : MonoBehaviour
    {
        [SerializeField] private EventReference bgmEventReference;

        void Start()
        {
            AudioManager.Instance.PlayBGM("event:/Music/Level 02");
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                AudioManager.Instance.PlayOneShot("event:/Weapons/Pistol");
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                AudioManager.Instance.PlayBGM(bgmEventReference);
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                AudioManager.Instance.PlayBGM("event:/Music/Radio Station", 1f, 2f);
            }

            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                AudioManager.Instance.StopBGM();
            }
        }
    }
}
