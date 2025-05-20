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
        // R キーで録音開始
        if (Input.GetKeyDown(KeyCode.R) && !isRecording)
        {
            StartRecording();
        }
        // S キーで録音停止
        if (Input.GetKeyDown(KeyCode.S) && isRecording)
        {
            StopRecording();
        }
    }

    void StartRecording()
    {
        // デフォルトマイクで録音開始、ループしない、最大 maxRecordDuration 秒
        audioSource.clip = Microphone.Start(null, false, maxRecordDuration, sampleRate);
        isRecording = true;

        // 録音が始まるまで待機
        while (!(Microphone.GetPosition(null) > 0)) { }
        audioSource.Play();  // 録音中の音声をリアルタイム再生したい場合
        Debug.Log("録音開始");
    }

    void StopRecording()
    {
        Microphone.End(null);  // 録音停止
        isRecording = false;
        audioSource.Stop();    // 再生も停止
        Debug.Log("録音停止");

        // 録音データは audioSource.clip に保存されている
        // 必要ならここでファイル出力などを行ってください
    }
}


