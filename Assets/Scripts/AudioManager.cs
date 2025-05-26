using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 音声録音・再生を管理するクラス
/// マイク音声 → PCM16変換 → チャンク送信
/// 受信音声 → AudioClip変換 → 再生
/// </summary>
public class AudioManager : MonoBehaviour
{
    [Header("音声設定")]
    [SerializeField] private int sampleRate = 24000; // OpenAI Realtime API推奨
    [SerializeField] private float chunkDurationMs = 50f; // 50msチャンク
    [SerializeField] private int recordingLength = 10; // 循環バッファ長(秒)
    
    [Header("必須コンポーネント")]
    [SerializeField] private AudioSource audioSource;
    
    [Header("デバッグ")]
    [SerializeField] private bool showDebugLog = true;
    
    // マイク関連
    private AudioClip microphoneClip;
    private string microphoneDevice;
    private bool isRecording = false;
    private Coroutine recordingCoroutine;
    
    // 音声チャンク送信イベント
    public event Action<byte[]> OnAudioChunkReady;
    
    // プロパティ
    public bool IsRecording => isRecording;
    public int SampleRate => sampleRate;
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        // AudioSource確保
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
    }
    
    private void Start()
    {
        InitializeMicrophone();
    }
    
    private void OnDestroy()
    {
        StopRecording();
    }
    
    #endregion
    
    #region マイク初期化
    
    /// <summary>
    /// マイクデバイスの初期化
    /// </summary>
    private void InitializeMicrophone()
    {
        if (Microphone.devices.Length > 0)
        {
            microphoneDevice = Microphone.devices[0];
            LogDebug($"使用マイク: {microphoneDevice}");
        }
        else
        {
            LogError("マイクデバイスが見つかりません");
            microphoneDevice = null;
        }
    }
    
    #endregion
    
    #region 録音制御
    
    /// <summary>
    /// 録音開始
    /// </summary>
    public void StartRecording()
    {
        if (isRecording)
        {
            LogDebug("既に録音中です");
            return;
        }
        
        if (string.IsNullOrEmpty(microphoneDevice))
        {
            LogError("マイクデバイスが利用できません");
            return;
        }
        
        try
        {
            // マイク録音開始
            microphoneClip = Microphone.Start(microphoneDevice, true, recordingLength, sampleRate);
            
            if (microphoneClip == null)
            {
                LogError("マイク録音の開始に失敗しました");
                return;
            }
            
            isRecording = true;
            
            // 音声チャンク送信コルーチン開始
            recordingCoroutine = StartCoroutine(SendAudioChunks());
            
            LogDebug("録音開始成功");
        }
        catch (Exception e)
        {
            LogError($"録音開始エラー: {e.Message}");
        }
    }
    
    /// <summary>
    /// 録音停止
    /// </summary>
    public void StopRecording()
    {
        if (!isRecording)
        {
            return;
        }
        
        isRecording = false;
        
        // コルーチン停止
        if (recordingCoroutine != null)
        {
            StopCoroutine(recordingCoroutine);
            recordingCoroutine = null;
        }
        
        // マイク停止
        if (!string.IsNullOrEmpty(microphoneDevice))
        {
            Microphone.End(microphoneDevice);
        }
        
        LogDebug("録音停止");
    }
    
    #endregion
    
    #region 音声チャンク送信
    
    /// <summary>
    /// 音声チャンクを定期的に送信するコルーチン
    /// </summary>
    private IEnumerator SendAudioChunks()
    {
        int lastSample = 0;
        int samplesPerChunk = Mathf.RoundToInt(sampleRate * (chunkDurationMs / 1000f));
        float waitTime = chunkDurationMs / 1000f;
        
        LogDebug($"チャンク送信開始 - サンプル/チャンク: {samplesPerChunk}, 間隔: {waitTime:F3}秒");
        
        while (isRecording && microphoneClip != null)
        {
            try
            {
                int currentSample = Microphone.GetPosition(microphoneDevice);
                
                // 循環バッファの処理
                if (currentSample < lastSample)
                {
                    // バッファが循環した場合、残りの部分を処理
                    int remainingSamples = microphoneClip.samples - lastSample;
                    if (remainingSamples > 0)
                    {
                        ProcessAudioChunk(lastSample, remainingSamples);
                    }
                    lastSample = 0;
                }
                
                // 新しいサンプルを処理
                if (currentSample > lastSample)
                {
                    int availanceSamples = currentSample - lastSample;
                    
                    while (availanceSamples >= samplesPerChunk)
                    {
                        ProcessAudioChunk(lastSample, samplesPerChunk);
                        lastSample += samplesPerChunk;
                        availanceSamples -= samplesPerChunk;
                    }
                }
            }
            catch (Exception e)
            {
                LogError($"音声チャンク処理エラー: {e.Message}");
            }
            
            yield return new WaitForSeconds(waitTime);
        }
        
        LogDebug("チャンク送信終了");
    }
    
    /// <summary>
    /// 指定位置から音声チャンクを処理
    /// </summary>
    private void ProcessAudioChunk(int startSample, int sampleCount)
    {
        try
        {
            // 音声サンプル取得
            float[] samples = new float[sampleCount];
            microphoneClip.GetData(samples, startSample);
            
            // PCM16に変換
            byte[] pcmData = ConvertToPCM16(samples);
            
            if (pcmData.Length > 0)
            {
                // イベント通知で送信
                OnAudioChunkReady?.Invoke(pcmData);
                LogDebug($"音声チャンク送信: {pcmData.Length} bytes");
            }
        }
        catch (Exception e)
        {
            LogError($"音声チャンク変換エラー: {e.Message}");
        }
    }
    
    #endregion
    
    #region 音声変換
    
    /// <summary>
    /// Unity float配列をPCM16バイト配列に変換
    /// </summary>
    private byte[] ConvertToPCM16(float[] samples)
    {
        byte[] pcmData = new byte[samples.Length * 2];
        
        for (int i = 0; i < samples.Length; i++)
        {
            // Unity音声範囲 (-1.0f ~ 1.0f) をPCM16範囲 (-32768 ~ 32767) に変換
            float clampedSample = Mathf.Clamp(samples[i], -1.0f, 1.0f);
            short pcm16Sample = (short)(clampedSample * 32767f);
            
            // Little Endian形式でバイト配列に格納
            pcmData[i * 2] = (byte)(pcm16Sample & 0xFF);           // 下位バイト
            pcmData[i * 2 + 1] = (byte)((pcm16Sample >> 8) & 0xFF); // 上位バイト
        }
        
        return pcmData;
    }
    
    /// <summary>
    /// PCM16バイト配列をUnity float配列に変換
    /// </summary>
    private float[] ConvertFromPCM16(byte[] pcmData)
    {
        if (pcmData.Length % 2 != 0)
        {
            LogError("無効なPCM16データ: バイト数が奇数です");
            return new float[0];
        }
        
        int sampleCount = pcmData.Length / 2;
        float[] samples = new float[sampleCount];
        
        for (int i = 0; i < sampleCount; i++)
        {
            // Little Endianバイト配列からshortに変換
            short pcm16Sample = (short)(pcmData[i * 2] | (pcmData[i * 2 + 1] << 8));
            
            // PCM16範囲をUnity範囲に変換
            samples[i] = pcm16Sample / 32768f;
        }
        
        return samples;
    }
    
    #endregion
    
    #region 音声再生
    
    /// <summary>
    /// 受信した音声データを再生
    /// </summary>
    public void PlayReceivedAudio(byte[] audioData)
    {
        if (audioData == null || audioData.Length == 0)
        {
            LogDebug("空の音声データは再生しません");
            return;
        }
        
        try
        {
            // PCM16データをAudioClipに変換
            AudioClip clip = CreateAudioClipFromPCM16(audioData);
            
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip);
                LogDebug($"音声再生: 長さ{clip.length:F2}秒");
            }
        }
        catch (Exception e)
        {
            LogError($"音声再生エラー: {e.Message}");
        }
    }
    
    /// <summary>
    /// PCM16データからAudioClipを作成
    /// </summary>
    private AudioClip CreateAudioClipFromPCM16(byte[] pcmData)
    {
        try
        {
            float[] samples = ConvertFromPCM16(pcmData);
            
            if (samples.Length == 0)
            {
                return null;
            }
            
            AudioClip clip = AudioClip.Create(
                name: $"ReceivedAudio_{DateTime.Now.Ticks}",
                lengthSamples: samples.Length,
                channels: 1, // モノラル
                frequency: sampleRate,
                stream: false
            );
            
            clip.SetData(samples, 0);
            return clip;
        }
        catch (Exception e)
        {
            LogError($"AudioClip作成エラー: {e.Message}");
            return null;
        }
    }
    
    #endregion
    
    #region 公開メソッド
    
    /// <summary>
    /// 利用可能なマイクデバイス一覧を取得
    /// </summary>
    public string[] GetAvailableMicrophones()
    {
        return Microphone.devices;
    }
    
    /// <summary>
    /// マイクデバイスを変更
    /// </summary>
    public void ChangeMicrophoneDevice(int deviceIndex)
    {
        if (deviceIndex >= 0 && deviceIndex < Microphone.devices.Length)
        {
            bool wasRecording = isRecording;
            
            if (wasRecording)
            {
                StopRecording();
            }
            
            microphoneDevice = Microphone.devices[deviceIndex];
            LogDebug($"マイクデバイス変更: {microphoneDevice}");
            
            if (wasRecording)
            {
                StartRecording();
            }
        }
    }
    
    /// <summary>
    /// 音声設定を動的に変更
    /// </summary>
    public void ChangeAudioSettings(int newSampleRate, float newChunkDuration)
    {
        bool wasRecording = isRecording;
        
        if (wasRecording)
        {
            StopRecording();
        }
        
        sampleRate = newSampleRate;
        chunkDurationMs = newChunkDuration;
        
        LogDebug($"音声設定変更: {sampleRate}Hz, {chunkDurationMs}ms");
        
        if (wasRecording)
        {
            StartRecording();
        }
    }
    
    #endregion
    
    #region デバッグ
    
    private void LogDebug(string message)
    {
        if (showDebugLog)
        {
            Debug.Log($"[AudioManager] {message}");
        }
    }
    
    private void LogError(string message)
    {
        Debug.LogError($"[AudioManager] {message}");
    }
    
    /// <summary>
    /// 現在の状態情報を取得
    /// </summary>
    public string GetStatusInfo()
    {
        return $"録音: {isRecording}, マイク: {microphoneDevice ?? "なし"}, サンプリング: {sampleRate}Hz";
    }
    
    #endregion
}