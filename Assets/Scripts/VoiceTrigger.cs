using UnityEngine;

public class VoiceTrigger : MonoBehaviour
{
    public VoicevoxTTS voicevox;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            voicevox.Speak("こんにちは、冥鳴ひまりです。ご気分はいかがですか？");
        }
    }
}

