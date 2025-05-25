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
    private const int micSampleRate = 16000;
    private bool isSending = false;

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
    }

    /* ------------ 接続開始 ------------ */
    public void StartRealtime()
    {
        Debug.Log("StartRealtime() 呼び出し");

        ws = new WebSocket(wsUrl);

        ws.OnOpen    += (s, e) => { Debug.Log("WebSocket opened");  if (statusText) statusText.text = "接続: オープン"; };
        ws.OnClose   += (s, e) => { Debug.Log("WebSocket closed");  if (statusText) statusText.text = "接続: クローズ"; };
        ws.OnError   += (s, e) => { if (ws == null || !ws.IsAlive) return;
                                    Debug.LogError("WebSocket error: " + e.Message);
                                    if (statusText) statusText.text = "エラー: " + e.Message; };
        ws.OnMessage += (s, e) => { if (ws == null || !ws.IsAlive) return;
                                    Debug.Log($"Received {e.RawData.Length} bytes");
                                    var clip = WavUtility.ToAudioClip(e.RawData, 1, "realtime");
                                    audioSource.PlayOneShot(clip); };

        ws.Connect();                        // 同期接続
        StartCoroutine(CaptureAndSend());    // マイク送信開始
    }

    /* ------------ 接続停止 ------------ */
    public void StopRealtime()
    {
        Debug.Log("StopRealtime() 呼び出し");
        isSending = false;

        if (ws != null)
        {
            ws.Close();     // もう生きていれば切断
            ws = null;
        }
    }

    /* ------------ マイク録音 → 送信 ------------ */
    private IEnumerator CaptureAndSend()
    {
        Debug.Log("CaptureAndSend 開始");

        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("マイクが見つかりません。");
            yield break;
        }

        micClip = Microphone.Start(null, true, 1, micSampleRate);

        int lastPos = 0;                       // ★ 先に宣言してから使う
        isSending  = true;

        while (isSending)
        {
            yield return null;

            // デバッグ: 現在のサンプル位置を毎フレーム確認
            Debug.Log($"mic pos={Microphone.GetPosition(null)}, lastPos={lastPos}");

            if (ws == null || !ws.IsAlive) break;

            int pos = Microphone.GetPosition(null);
            if (pos > lastPos)
            {
                var samples = new float[pos - lastPos];
                micClip.GetData(samples, lastPos);
                lastPos = pos;

                byte[] bytes = WavUtility.FromAudioClipSegment(samples, micSampleRate);
                if (bytes.Length == 0) continue;

                ws.Send(bytes);
                Debug.Log($"###SEND {bytes.Length} bytes");
            }
        }

        Microphone.End(null);
        Debug.Log("CaptureAndSend 終了");
    }
}
