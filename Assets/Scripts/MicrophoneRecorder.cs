using UnityEngine;
using System.Collections;

public class MicrophoneRecorder : MonoBehaviour
{
    private AudioSource audioSource;
    private bool isRecording = false;

    // 録音時の設定
    public int sampleRate = 44100;    // サンプリングレート
    public int maxRecordDuration = 10; // 最大録音時間（秒）

    void Start()
    {
        // AudioSource コンポーネントを参照
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R) && !isRecording)
        {
            StartRecording();
        }
        if (Input.GetKeyDown(KeyCode.S) && isRecording)
        {
            StopRecording();
        }
    }

    void StartRecording()
    {
        audioSource.clip = Microphone.Start(null, false, maxRecordDuration, sampleRate);
        isRecording = true;
        while (!(Microphone.GetPosition(null) > 0)) { }
        audioSource.Play();
        Debug.Log("録音開始");
    }

    void StopRecording()
    {
        Microphone.End(null);
        isRecording = false;
        audioSource.Stop();

        Debug.Log("録音停止");

        string path = Application.persistentDataPath + "/recorded.wav";
        SaveAudioClipAsWav(audioSource.clip, path);
        StartCoroutine(TranscribeAndRespond(path));
    }

    // ✅ ここが実際に .wav を保存する関数（クラスの中にある）
    void SaveAudioClipAsWav(AudioClip clip, string path)
    {
        WavUtility.FromAudioClip(clip, path, true);
        Debug.Log("WAVファイルを保存しました: " + path);
    }

    IEnumerator TranscribeAndRespond(string path)
    {
        Debug.Log("Transcribe from: " + path);
        yield return null;
    }
}  // ←ここでクラスは終了

