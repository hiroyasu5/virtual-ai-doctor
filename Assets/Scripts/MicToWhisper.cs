using UnityEngine;
using Whisper;
using System.Linq;
using System.Threading.Tasks;
using TMPro;  // ← TextMeshPro を使うなら

public class MicToWhisper : MonoBehaviour
{
    [Header("Whisper Manager")]
    public WhisperManager whisperManager;   // シーン上の WhisperManagerObject をドラッグ

    [Header("録音設定")]
    public int recordSeconds = 10;           // 録音秒数
    public int sampleRate   = 16000;         // Whisper 推奨 16kHz

    [Header("UI")]
    public TextMeshProUGUI transcriptText;   // Hierarchy の TranscriptText をドラッグ

    private string    micName;
    private AudioClip recordedClip;
    private bool      isRecording = false;

    async void Awake()
    {
        // Init On Awake にチェックが入っていればモデルは自動ロード済み
        // 結果のセグメントを逐次コンソールにも表示
        whisperManager.OnNewSegment += seg =>
        {
            Debug.Log($"[seg] {seg.Text}");
        };
    }

    void Start()
    {
        // マイクデバイスを取得
        if (Microphone.devices.Length > 0)
            micName = Microphone.devices[0];
        else
            Debug.LogError("マイクが見つかりません。");
    }

    void Update()
    {
        // Rキーで録音開始
        if (Input.GetKeyDown(KeyCode.R) && !isRecording)
        {
            recordedClip = Microphone.Start(micName, false, recordSeconds, sampleRate);
            isRecording = true;
            transcriptText.text = "Recording…";
            Debug.Log("録音開始");
        }

        // Sキーで録音停止 → 文字起こし
        if (Input.GetKeyDown(KeyCode.S) && isRecording)
        {
            Microphone.End(micName);
            isRecording = false;
            transcriptText.text = "Writing…";
            Debug.Log("録音停止 → 文字起こし開始");
            _ = TranscribeAsync();
        }
    }

    private async Task TranscribeAsync()
    {
        var result = await whisperManager.GetTextAsync(recordedClip);
        if (result == null)
        {
            Debug.LogError("Failed");
            transcriptText.text = "失敗";
            return;
        }

        // 各セグメントの Text をつなげて全文を取得
        string full = string.Join(" ",
            result.Segments.Select(s => s.Text)
        );

        Debug.Log("文字起こし結果: " + full);
        transcriptText.text = full;
    }
}
