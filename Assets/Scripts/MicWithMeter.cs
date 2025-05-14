using System.Collections;        // Coroutine 用
using UnityEngine;
using UnityEngine.UI;  // UI.Image を使うときに必要

public class MicWithMeter : MonoBehaviour
{
    [Header("録音設定")]
    public string micName;
    public int sampleRate    = 16000;  // Whisper推奨 16kHz
    public int recordSeconds = 10;     // バッファ長

    [Header("UI")]
    public Image levelMeter;           // Canvas 上の Image をドラッグ

    private AudioSource audioSource;
    private bool        isRecording = false;

    void Start()
    {
        // AudioSource を追加し、マイク音声をループ再生（ミュート）
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.mute = true;

        // マイクデバイスの取得
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
            audioSource.clip = Microphone.Start(micName, true, recordSeconds, sampleRate);
            audioSource.Play();
            isRecording = true;
            Debug.Log("録音開始");
        }

        // Sキーで録音停止
        if (Input.GetKeyDown(KeyCode.S) && isRecording)
        {
            Microphone.End(micName);
            audioSource.Stop();
            isRecording = false;
            Debug.Log("録音停止");
        }

        // 録音の有無にかかわらず毎フレームメーターを更新
        UpdateMeter();
    }

    void UpdateMeter()
    {
        // 録音していないときは空状態を表示
        if (audioSource.clip == null)
        {
            levelMeter.fillAmount = 0f;
            return;
        }

        // 実際の波形を取得してRMSを計算
        float[] samples = new float[256];
        audioSource.GetOutputData(samples, 0);
        float sumSq = 0f;
        foreach (var s in samples) sumSq += s * s;
        float rms = Mathf.Sqrt(sumSq / samples.Length);

        // 感度補正
        float sensitivity = 10f;
        float level = Mathf.Clamp01(rms * sensitivity);

        // UIに反映
        levelMeter.fillAmount = level;
    }
}
