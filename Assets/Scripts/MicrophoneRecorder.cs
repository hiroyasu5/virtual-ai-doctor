using UnityEngine;
using UnityEngine.Networking;
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
    public string userApiKey = "";   // 空文字ならサーバー側の環境変数を使います

    void Start()
    {
        // AudioSource がなければ追加
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

        // 録音スタート待ち
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

        // WAV を保存して…
        string filePath = Path.Combine(Application.persistentDataPath, "recorded.wav");
        SaveAudioClipAsWav(audioSource.clip, filePath);
        Debug.Log("WAVファイル保存完了: " + filePath);

        // サーバーへ送信して文字起こし＆ChatGPT
        StartCoroutine(UploadAndTranscribe(filePath));
    }

    void SaveAudioClipAsWav(AudioClip clip, string path)
    {
        if (clip == null)
        {
            Debug.LogError("録音データがありません");
            return;
        }
        // byte[] 版を呼び出して自分で書き出し
        byte[] wavData = WavUtility.FromAudioClip(clip);
        File.WriteAllBytes(path, wavData);
    }

    // ────────────── ここから追加部分 ──────────────

    IEnumerator UploadAndTranscribe(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("ファイルが見つかりません: " + filePath);
            yield break;
        }

        // Whisper 用フォーム
        byte[] bytes = File.ReadAllBytes(filePath);
        WWWForm form = new WWWForm();
        form.AddBinaryData("audio", bytes, "recorded.wav", "audio/wav");
        if (!string.IsNullOrEmpty(userApiKey))
            form.AddField("user_api_key", userApiKey);

        string urlT = serverBaseUrl.TrimEnd('/') + "/transcribe";
        using (var www = UnityWebRequest.Post(urlT, form))
        {
            yield return www.SendWebRequest();
            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("文字起こし失敗: " + www.error);
                Debug.LogError("レスポンス: " + www.downloadHandler.text);
                yield break;
            }

            var respT = JsonUtility.FromJson<TranscribeResponse>(www.downloadHandler.text);
            Debug.Log("📝 Whisper結果: " + respT.text);

            // ChatGPT へ
            yield return StartCoroutine(SendChat(new ChatMessage[]{
                new ChatMessage { role="user", content=respT.text }
            }));
        }
    }

    IEnumerator SendChat(ChatMessage[] messages)
    {
        var chatReq = new ChatRequest { messages = messages, user_api_key = userApiKey };
        string json = JsonUtility.ToJson(chatReq);

        var req = new UnityWebRequest(serverBaseUrl.TrimEnd('/') + "/chat", "POST");
        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("ChatGPT リクエスト失敗: " + req.error);
            Debug.LogError("レスポンス: " + req.downloadHandler.text);
            yield break;
        }

        var respC = JsonUtility.FromJson<ChatResponse>(req.downloadHandler.text);
        string reply = respC.choices[0].message.content;
        Debug.Log("🤖 ChatGPT 返答: " + reply);

        // ▶︎ ここで reply を画面 UI や VoiceVox などに渡す
    }

    [System.Serializable]
    private class TranscribeResponse { public string text; }

    [System.Serializable]
    private class ChatRequest
    {
        public ChatMessage[] messages;
        public string user_api_key;
    }

    [System.Serializable]
    private class ChatMessage
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    private class ChatResponse { public Choice[] choices; }
    [System.Serializable]
    private class Choice { public ChatMessage message; }
}


