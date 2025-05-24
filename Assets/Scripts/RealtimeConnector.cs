using UnityEngine;
using WebSocketSharp;
using System.Collections;
using TMPro;

public class RealtimeConnector : MonoBehaviour
{
    [Tooltip("例: ws://localhost:8000/ws/realtime")]
    public string wsUrl = "ws://localhost:8000/ws/realtime";
    public TMP_Text statusText;

    private WebSocket ws;
    private AudioSource audioSource;
    private AudioClip micClip;
    private int micSampleRate = 16000;
    private bool isSending = false;

    void Awake()
    {
        // 再生用 AudioSource を追加
        audioSource = gameObject.AddComponent<AudioSource>();
    }

    /// <summary>リアルタイム会話を開始</summary>
    public void StartRealtime()
    {
        Debug.Log("StartRealtime() 呼び出し");

        ws = new WebSocket(wsUrl);

        ws.OnOpen += (s, e) =>
        {
            Debug.Log("WebSocket opened");
            if (statusText) statusText.text = "接続: オープン";
        };

        ws.OnClose += (s, e) =>
        {
            Debug.Log("WebSocket closed");
            if (statusText) statusText.text = "接続: クローズ";
        };

        ws.OnError += (s, e) =>
        {
            if (ws == null || !ws.IsAlive) return;
            Debug.LogError("WebSocket error: " + e.Message);
            if (statusText) statusText.text = "エラー: " + e.Message;
        };

        ws.OnMessage += (s, e) =>
        {
            if (ws == null || !ws.IsAlive) return;
            Debug.Log($"Received {e.RawData.Length} bytes");
            var clip = WavUtility.ToAudioClip(e.RawData, 1, "realtime");
            audioSource.PlayOneShot(clip);
        };

        // 同期的に接続開始
        ws.Connect();
        // 録音→送信コルーチンを起動
        StartCoroutine(CaptureAndSend());
    }

    /// <summary>リアルタイム会話を停止</summary>
    public void StopRealtime()
    {
        Debug.Log("StopRealtime() 呼び出し");
        isSending = false;

        if (ws != null)
        {
            // イベントハンドラを解除
            ws.OnOpen    -= null;
            ws.OnClose   -= null;
            ws.OnError   -= null;
            ws.OnMessage -= null;

            if (ws.IsAlive)
                ws.Close();

            ws = null;
        }
    }

    private IEnumerator CaptureAndSend()
    {
        Debug.Log("CaptureAndSend 開始");

        // ◆ エディター上ではマイク操作をスキップ
        #if UNITY_EDITOR
        Debug.LogWarning("エディター環境ではマイク入力をスキップします");
        yield break;
        #endif

        // 実機ビルド向け：マイクデバイスがなければ中断
        if (Microphone.devices == null || Microphone.devices.Length == 0)
        {
            Debug.LogWarning("マイクが見つかりません。CaptureAndSend を中断します。");
            yield break;
        }

        // マイク録音を開始 (1秒ループ, 16kHz)
        micClip = Microphone.Start(null, true, 1, micSampleRate);

        int lastPos = 0;
        isSending = true;

        while (isSending)
        {
            yield return null;

            if (ws == null || !ws.IsAlive)
                break;

            int pos = Microphone.GetPosition(null);
            if (pos > lastPos)
            {
                var samples = new float[pos - lastPos];
                micClip.GetData(samples, lastPos);
                lastPos = pos;

                byte[] bytes = WavUtility.FromAudioClipSegment(samples, micSampleRate);
                try
                {
                    ws.Send(bytes);
                    Debug.Log($"Sent {bytes.Length} bytes");
                }
                catch
                {
                    Debug.Log("WebSocket が閉じられたため送信中断");
                    break;
                }
            }
        }

        // 録音停止
        Microphone.End(null);
        Debug.Log("CaptureAndSend 終了");
    }

} // ← ここがクラスの閉じ括弧です
