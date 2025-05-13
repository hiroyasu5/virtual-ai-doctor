using UnityEngine;
using Whisper;

public class MicToWhisper : MonoBehaviour
{
    private AudioClip recordedClip;
    private string micName;
    private AudioSource audioSource;
    private bool isRecording = false;

    public WhisperManager whisperManager;
    public string modelName = "for-tests-ggml-tiny.bin"; // StreamingAssetsに置いたファイル名

    void Start()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        whisperManager.Init(modelName);

        if (Microphone.devices.Length > 0)
        {
            micName = Microphone.devices[0];
            Debug.Log("マイク：" + micName);
        }
        else
        {
            Debug.LogWarning("マイクが見つかりません！");
        }
    }

    void Update()
    {
        // Rキーで録音開始
        if (Input.GetKeyDown(KeyCode.R) && !isRecording)
        {
            Debug.Log("録音開始！");
            recordedClip = Microphone.Start(micName, false, 10, 44100);
            isRecording = true;
        }

        // Sキーで録音終了 → Whisperへ送る
        if (Input.GetKeyDown(KeyCode.S) && isRecording)
        {
            Microphone.End(micName);
            isRecording = false;
            Debug.Log("録音終了、テキスト変換開始...");

            audioSource.clip = recordedClip;
            audioSource.Play(); // 確認用に再生

            // Whisperで音声→テキスト変換（非同期）
            whisperManager.Transcribe(recordedClip, OnTranscriptionComplete);
        }
    }

    // Whisper 変換完了時に呼ばれるコールバック
    private void OnTranscriptionComplete(string result)
    {
        Debug.Log("文字起こし結果: " + result);
        // 必要があれば UI テキストに反映も可
    }
}
