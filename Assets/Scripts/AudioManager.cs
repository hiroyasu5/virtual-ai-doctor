using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 音声録音・再生を管理するクラス
/// マイク音声 → PCM16変換 → チャンク送信
/// 受信音声 → AudioClip変換 → 連続再生
/// </summary>
public class AudioManager : MonoBehaviour
{
    [Header("音声設定")]
    [SerializeField] private int sampleRate = 16000; // ✅ OpenAI Realtime API標準仕様
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
    
    // 🎵 音声再生バッファ（リングバッファ方式）
    private AudioClip streamingClip;
    private float[] audioBuffer;
    private int bufferWritePosition = 0;
    private int bufferReadPosition = 0;  // ← これを追加
    private int bufferSize;
    private bool isStreaming = false;
    
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
        
        // ✅ ストリーミング音声バッファ初期化
        InitializeStreamingBuffer();
    }
    
    private void Start()
    {
        InitializeMicrophone();
    }
    
    private void OnDestroy()
    {
        StopRecording();
        StopStreaming();
    }
    
    #endregion
    
    #region ストリーミング音声バッファ
    
    /// <summary>
    /// ストリーミング音声バッファの初期化
    /// </summary>
    private void InitializeStreamingBuffer()
    {
        // 3秒分のバッファを用意（16kHz × 3秒）
        bufferSize = sampleRate * 3;
        audioBuffer = new float[bufferSize];
        
        // ストリーミング用AudioClip作成
        streamingClip = AudioClip.Create(
            "StreamingAudio",
            bufferSize,
            1, // モノラル
            sampleRate,
            true, // ストリーミング
            OnAudioRead
        );
        
        LogDebug($"ストリーミングバッファ初期化: {bufferSize} samples ({bufferSize / (float)sampleRate:F1}秒)");
    }
    
    /// <summary>
    /// AudioClipストリーミング用コールバック
    /// </summary>
    /// <param name="data">出力音声データ</param>
    private void OnAudioRead(float[] data)
{
    if (!isStreaming || audioBuffer == null)
    {
        // 無音で埋める
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = 0f;
        }
        return;
    }
    
    lock (_streamLock)
    {
        // シンプルな読み取り
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = audioBuffer[bufferReadPosition] * 0.3f; // 音量を30%に
            bufferReadPosition = (bufferReadPosition + 1) % bufferSize;
        }
    }
}
    
    /// <summary>
    /// ストリーミング開始
    /// </summary>
    private void StartStreaming()
{
    if (!isStreaming && streamingClip != null)
    {
        isStreaming = true;
        audioSource.clip = streamingClip;
        audioSource.loop = true;
        audioSource.volume = 0.5f;  // ← 音量を50%に
        audioSource.Play();
        LogDebug($"ストリーミング音声開始 - SampleRate: {streamingClip.frequency}Hz");
    }
}
    
    /// <summary>
    /// ストリーミング停止
    /// </summary>
    private void StopStreaming()
    {
        if (isStreaming)
        {
            isStreaming = false;
            audioSource.Stop();
            audioSource.clip = null;
            LogDebug("ストリーミング音声停止");
        }
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
        
        // ✅ 16kHz × 50ms = 800サンプル × 2bytes = 1600 bytes期待
        LogDebug($"チャンク送信開始 - サンプル/チャンク: {samplesPerChunk}, 期待バイト数: {samplesPerChunk * 2}, 間隔: {waitTime:F3}秒");
        
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
                    int availableSamples = currentSample - lastSample;
                    
                    while (availableSamples >= samplesPerChunk)
                    {
                        ProcessAudioChunk(lastSample, samplesPerChunk);
                        lastSample += samplesPerChunk;
                        availableSamples -= samplesPerChunk;
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
                LogDebug($"音声チャンク送信: {pcmData.Length} bytes"); // 1600 bytes期待
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
/// 受信した音声データを再生（ストリーミング方式）
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
        // AudioSourceチェック
        if (audioSource == null)
        {
            LogError("AudioSourceが設定されていません！");
            return;
        }
        
        // PCM16データをfloat配列に変換
        float[] samples = ConvertFromPCM16(audioData);
        
        if (samples.Length == 0)
        {
            LogError("音声変換に失敗しました");
            return;
        }
        
        // 音声レベルチェック（デバッグ用）
        float maxAmplitude = 0f;
        float avgAmplitude = 0f;
        for (int i = 0; i < Mathf.Min(samples.Length, 1000); i++)
        {
            float abs = Mathf.Abs(samples[i]);
            maxAmplitude = Mathf.Max(maxAmplitude, abs);
            avgAmplitude += abs;
        }
        avgAmplitude /= Mathf.Min(samples.Length, 1000);
        
        LogDebug($"📊 音声レベル - 最大: {maxAmplitude:F4}, 平均: {avgAmplitude:F4}, サンプル数: {samples.Length}");
        
        // AudioSourceの状態確認
        LogDebug($"🔊 AudioSource状態 - Volume: {audioSource.volume}, Mute: {audioSource.mute}, isPlaying: {audioSource.isPlaying}");
        
        // ✅ ストリーミングバッファに書き込み
        WriteToStreamingBuffer(samples);
        
        // ストリーミング開始（初回のみ）
        if (!isStreaming)
        {
            StartStreaming();
        }
        
        LogDebug($"✅ 音声ストリーミング: {samples.Length} samples, {audioData.Length} bytes");
    }
    catch (Exception e)
    {
        LogError($"音声再生エラー: {e.Message}\nStackTrace: {e.StackTrace}");
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
        return $"録音: {isRecording}, マイク: {microphoneDevice ?? "なし"}, サンプリング: {sampleRate}Hz, ストリーミング: {isStreaming}";
    }
    
    #endregion

    [ContextMenu("Test Audio Playback")]
private void TestAudioPlayback()
{
    if (audioSource == null)
    {
        LogError("AudioSourceが設定されていません！");
        return;
    }
    
    // AudioSourceの設定を確認
    audioSource.volume = 1.0f;
    audioSource.mute = false;
    
    // テスト用のサイン波を生成（440Hz、1秒）
    float[] testSamples = new float[sampleRate];
    for (int i = 0; i < testSamples.Length; i++)
    {
        testSamples[i] = Mathf.Sin(2 * Mathf.PI * 440 * i / (float)sampleRate) * 0.5f;
    }
    
    // PCM16に変換してから再生
    byte[] testPCM = ConvertToPCM16(testSamples);
    PlayReceivedAudio(testPCM);
    
    LogDebug($"🎵 テスト音声を再生しました（440Hz, 1秒, {testPCM.Length} bytes）");
}

[ContextMenu("Check Audio System")]
private void CheckAudioSystem()
{
    LogDebug("=== Audio System Check ===");
    LogDebug($"AudioSource: {(audioSource != null ? "OK" : "NULL")}");
    
    if (audioSource != null)
    {
        LogDebug($"- Volume: {audioSource.volume}");
        LogDebug($"- Mute: {audioSource.mute}");
        LogDebug($"- isPlaying: {audioSource.isPlaying}");
        LogDebug($"- clip: {(audioSource.clip != null ? audioSource.clip.name : "NULL")}");
    }
    
    LogDebug($"StreamingClip: {(streamingClip != null ? "OK" : "NULL")}");
    LogDebug($"isStreaming: {isStreaming}");
    LogDebug($"Sample Rate: {sampleRate}");
    LogDebug($"Buffer Size: {bufferSize}");
    
    // Unity全体の音声設定
    LogDebug($"Unity AudioListener Volume: {AudioListener.volume}");
    LogDebug($"Unity AudioListener Pause: {AudioListener.pause}");
}
}
