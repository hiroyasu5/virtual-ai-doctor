using UnityEngine;
using UnityEngine.Networking;    // ← 追加
using System.Collections;
using System.IO;

public class MicrophoneRecorder : MonoBehaviour
{
    private AudioSource audioSource;
    private bool isRecording = false;

    [Header("録音の設定")]
    public int sampleRate = 44100;        // サンプリングレート
    public int maxRecordDuration = 10;    // 最大録音時間(秒)

    [Header("サーバー設定")]
    [Tooltip("あなたの Render サーバー URL")]
    public string serverBaseUrl = "https://ai-relay-server.onrender.com";
    [Tooltip("自分の APIキーを送信したい場合")]
    public string userApiKey = "";   // 空文字なら環境変数のキーを使います

    void Start()
    {
        // AudioSource が無ければ追加
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        // Rキーで録音開始
        if (Input.GetKeyDown(KeyCode.R) && !isRecording)
            StartCoroutine(StartRecording());

        // Sキーで録音停止
        if (Input.GetKeyDown(KeyCode.S) && isRecording)
            StopRecording();
    }

    IEnumerator StartRecording()
    {
        audioSource.clip = Microphone.Start(null, false, maxRecordDuration, sampleRate);
        isRecording = true;

        // 録音スタートまで待つ
        while (Microphone.GetPosition(null) <= 0)
            yield return null;

        audioSource.Play();
        Debug.Log("録音開始");
    }

    void StopRecording()
    {
        Microphone.End(null);
        isRecording = false;
        audioSource.Stop();
        Debug.Log("録音停止");

        // パスを組み立てて WAV 保存
        string filePath = Path.Combine(Application.persistentDataPath, "recorded.wav");
        SaveAudioClipAsWav(audioSource.clip, filePath);
        Debug.Log("WAVファイル保存完了: " + filePath);

        // サーバーにアップロードして文字起こし
        StartCoroutine(UploadAndTranscribe(filePath));
    }

    void SaveAudioClipAsWav(AudioClip clip, string path)
    {
        if (clip == null)
        {
            Debug.LogError("AudioClip が null です。録音に失敗している可能性があります。");
            return;
        }

        // 引数1つの版を呼び出し byte[] を受け取る
        byte[] wavData = WavUtility.FromAudioClip(clip);

        // ファイル書き出し
        File.WriteAllBytes(path, wavData);
    }

    /// <summary>
    /// サーバーの /transcribe に WAV を送信 → whisper で文字起こし → 結果を受け取る
    /// </summary>
    IEnumerator UploadAndTranscribe(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("送信ファイルが見つかりません: " + filePath);
            yield break;
        }

        // バイナリを読み込んでフォームに詰める
        byte[] wavBytes = File.ReadAllBytes(filePath);
        WWWForm form = new WWWForm();
        form.AddBinaryData("audio", wavBytes, "recorded.wav", "audio/wav");

        // APIキーをフォームで送る (空文字なら省略ＯＫ)
        if (!string.IsNullOrEmpty(userApiKey))
            form.AddField("user_api_key", userApiKey);

        // リクエスト実行
        string url = serverBaseUrl.TrimEnd('/') + "/transcribe";
        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("文字起こしリクエスト失敗: " + www.error);
                Debug.LogError("レスポンス: " + www.downloadHandler.text);
                yield break;
            }

            // 成功時は JSON をパースしてログ出力
            var json = www.downloadHandler.text;
            var resp = JsonUtility.FromJson<TranscribeResponse>(json);
            Debug.Log("📝 Whisper文字起こし結果: " + resp.text);

            // ここで ChatGPT 呼び出しに繋いでもOK
        }
    }

    // /transcribe の返り値 JSON 用クラス
    [System.Serializable]
    private class TranscribeResponse
    {
        public string text;
    }
}


