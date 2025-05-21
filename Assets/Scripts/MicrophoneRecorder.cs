using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;
using System.Text;   // ← これを追加

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

    [Header("Voicevox エンジン設定")]
    [Tooltip("Voicevox Engine が動いている URL")]
    public string voicevoxBaseUrl = "https://docter1-3.onrender.com";  // ← 自分の Voicevox サービス URL
    [Tooltip("冥鳴ひまり の speaker ID (例:14)")]
    public int voicevoxSpeakerId = 14;

    void Start()
    {
        audioSource = GetComponent<AudioSource>() 
                      ?? gameObject.AddComponent<AudioSource>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R) && !isRecording)
            StartCoroutine(StartRecording());

        if (Input.GetKeyDown(KeyCode.S) && isRecording)
            StopRecording();
    }

    IEnumerator StartRecording()
    {
        audioSource.clip = Microphone.Start(null, false, maxRecordDuration, sampleRate);
        isRecording = true;
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

        // 録音データを保存
        string filePath = Path.Combine(Application.persistentDataPath, "recorded.wav");
        byte[] wavData = WavUtility.FromAudioClip(audioSource.clip);
        File.WriteAllBytes(filePath, wavData);
        Debug.Log("WAVファイル保存完了: " + filePath);

        // Whisper → ChatGPT → Voicevox の一連フロー
        StartCoroutine(UploadAndTranscribe(filePath));
    }

    IEnumerator UploadAndTranscribe(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("ファイルが見つかりません: " + filePath);
            yield break;
        }

        // Whisper へ送信
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
        byte[] body = Encoding.UTF8.GetBytes(json);
        req.uploadHandler   = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("ChatGPT リクエスト失敗: " + req.error);
            yield break;
        }

        var respC = JsonUtility.FromJson<ChatResponse>(req.downloadHandler.text);
        string reply = respC.choices[0].message.content;
        Debug.Log("🤖 ChatGPT 返答: " + reply);

        // ここで Voicevox に渡す
        yield return StartCoroutine(GenerateVoice(reply));
    }

    IEnumerator GenerateVoice(string text)
    {
        // 1) audio_query
        string qUrl = $"{voicevoxBaseUrl.TrimEnd('/')}/audio_query" +
                      $"?text={UnityWebRequest.EscapeURL(text)}" +
                      $"&speaker={voicevoxSpeakerId}";
        using (var qReq = UnityWebRequest.Get(qUrl))
        {
            yield return qReq.SendWebRequest();
            if (qReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Voicevox /audio_query エラー: " + qReq.error);
                yield break;
            }
            string queryJson = qReq.downloadHandler.text;

            // 2) synthesis
            string sUrl = $"{voicevoxBaseUrl.TrimEnd('/')}/synthesis?speaker={voicevoxSpeakerId}";
            var sReq = new UnityWebRequest(sUrl, "POST");
            byte[] body = Encoding.UTF8.GetBytes(queryJson);
            sReq.uploadHandler   = new UploadHandlerRaw(body);
            sReq.downloadHandler = new DownloadHandlerBuffer();
            sReq.SetRequestHeader("Content-Type", "application/json");
            yield return sReq.SendWebRequest();

            if (sReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Voicevox /synthesis エラー: " + sReq.error);
                yield break;
            }

            // 3) 再生
            byte[] wavBytes = sReq.downloadHandler.data;
            AudioClip clip = WavUtility.ToAudioClip(wavBytes, 0, "voicevox");
            audioSource.clip = clip;
            audioSource.Play();
        }
    }

    [System.Serializable] private class TranscribeResponse { public string text; }
    [System.Serializable] private class ChatRequest { public ChatMessage[] messages; public string user_api_key; }
    [System.Serializable] private class ChatMessage { public string role; public string content; }
    [System.Serializable] private class ChatResponse { public Choice[] choices; }
    [System.Serializable] private class Choice { public ChatMessage message; }
}
