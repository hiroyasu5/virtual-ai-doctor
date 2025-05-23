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

        // 新しい WebSocket を生成
        ws = new WebSocket(wsUrl);

        // 接続成功
        ws.OnOpen += (s, e) =>
        {
            Debug.Log("WebSocket opened");
            if (statusText) statusText.text = "接続: オープン";
        };

        // 接続切断
        ws.OnClose += (s, e) =>
        {
            Debug.Log("WebSocket closed");
            if (statusText) statusText.text = "接続: クローズ";
        };

        // エラー発生時
        ws.OnError += (s, e) =>
        {
            // 切断済み or 未接続なら無視
            if (ws == null || !ws.IsAlive) return;
            Debug.LogError("WebSocket error: " + e.Message);
            if (statusText) statusText.text = "エラー: " + e.Message;
        };

        // メッセージ受信時
        ws.OnMessage += (s, e) =>
        {
            // 切断済み or 未接続なら無視
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
        // 送信ループを止めるフラグを倒す
        isSending = false;

        if (ws != null)
        {
            // イベントハンドラを解除してから切断
            ws.OnOpen    -= null;
            ws.OnClose   -= null;
            ws.OnError   -= null;
            ws.OnMessage -= null;

            if (ws.IsAlive)
                ws.Close();

            // 参照クリア
            ws = null;
        }
    }

    private IEnumerator CaptureAndSend()
    {
        Debug.Log("CaptureAndSend 開始");
        // マイク録音を開始 (1sec ループ, 16kHz)
        micClip = Microphone.Start(null, true, 1, micSampleRate);
        int lastPos = 0;
        isSending = true;

        while (isSending)
        {
            yield return null;

            // 切断済み or 未接続なら即ループ脱出
            if (ws == null || !ws.IsAlive)
                break;

            int pos = Microphone.GetPosition(null);
            if (pos > lastPos)
            {
                // 新規サンプルだけ取得
                var samples = new float[pos - lastPos];
                micClip.GetData(samples, lastPos);
                lastPos = pos;

                // PCM16bit バイト列に変換
                byte[] bytes = WavUtility.FromAudioClipSegment(samples, micSampleRate);

                // 送信
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
}
