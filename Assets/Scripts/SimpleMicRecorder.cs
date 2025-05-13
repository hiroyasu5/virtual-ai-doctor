using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimpleMicRecorder : MonoBehaviour
{
    private AudioClip recordedClip;
    private string micName;
    private AudioSource audioSource;

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();

        if (Microphone.devices.Length > 0)
        {
            micName = Microphone.devices[0];
            Debug.Log("使うマイク：" + micName);
            recordedClip = Microphone.Start(micName, false, 10, 44100);
        }
        else
        {
            Debug.LogWarning("マイクが見つかりません。");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (Microphone.IsRecording(micName))
            {
                Microphone.End(micName);
                audioSource.clip = recordedClip;
                audioSource.Play();
                Debug.Log("録音再生！");
            }
        }
    }
}
