using System;
using UnityEngine;

/// <summary>
/// 音声データフォーマット変換クラス
/// Unity AudioClip ⇔ PCM16 ⇔ OpenAI音声形式
/// </summary>
public static class AudioConverter
{
    /// <summary>
    /// Unity float配列をPCM16バイト配列に変換
    /// </summary>
    /// <param name="samples">Unity音声サンプル (-1.0f ~ 1.0f)</param>
    /// <returns>PCM16バイト配列</returns>
    public static byte[] ConvertToPCM16(float[] samples)
    {
        if (samples == null || samples.Length == 0)
        {
            Debug.LogWarning("AudioConverter: 空の音声サンプルです");
            return new byte[0];
        }

        byte[] pcmData = new byte[samples.Length * 2]; // 16bit = 2bytes per sample
        
        for (int i = 0; i < samples.Length; i++)
        {
            // Unity音声範囲 (-1.0f ~ 1.0f) を PCM16範囲 (-32768 ~ 32767) に変換
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
    /// <param name="pcmData">PCM16バイト配列</param>
    /// <returns>Unity音声サンプル配列</returns>
    public static float[] ConvertFromPCM16(byte[] pcmData)
    {
        if (pcmData == null || pcmData.Length == 0 || pcmData.Length % 2 != 0)
        {
            Debug.LogWarning("AudioConverter: 無効なPCM16データです");
            return new float[0];
        }
        
        int sampleCount = pcmData.Length / 2;
        float[] samples = new float[sampleCount];
        
        for (int i = 0; i < sampleCount; i++)
        {
            // Little Endianバイト配列からshortに変換
            short pcm16Sample = (short)(pcmData[i * 2] | (pcmData[i * 2 + 1] << 8));
            
            // PCM16範囲 (-32768 ~ 32767) をUnity範囲 (-1.0f ~ 1.0f) に変換
            samples[i] = pcm16Sample / 32768f;
        }
        
        return samples;
    }
    
    /// <summary>
    /// バイト配列からUnity AudioClipを生成
    /// </summary>
    /// <param name="audioData">音声バイナリデータ</param>
    /// <param name="sampleRate">サンプリングレート</param>
    /// <param name="channels">チャンネル数</param>
    /// <returns>Unity AudioClip</returns>
    public static AudioClip CreateAudioClipFromBytes(byte[] audioData, int sampleRate = 24000, int channels = 1)
    {
        if (audioData == null || audioData.Length == 0)
        {
            Debug.LogWarning("AudioConverter: 空の音声データです");
            return null;
        }
        
        try
        {
            // PCM16データをfloat配列に変換
            float[] samples = ConvertFromPCM16(audioData);
            
            if (samples.Length == 0)
            {
                Debug.LogWarning("AudioConverter: 音声変換に失敗しました");
                return null;
            }
            
            // AudioClip作成
            AudioClip audioClip = AudioClip.Create(
                name: "ReceivedAudio_" + DateTime.Now.Ticks,
                lengthSamples: samples.Length / channels,
                channels: channels,
                frequency: sampleRate,
                stream: false
            );
            
            // 音声データをAudioClipに設定
            audioClip.SetData(samples, 0);
            
            Debug.Log($"AudioConverter: AudioClip作成完了 - 長さ: {audioClip.length:F2}秒");
            return audioClip;
        }
        catch (Exception e)
        {
            Debug.LogError($"AudioConverter: AudioClip作成エラー - {e.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// AudioClipから50msチャンクを切り出し
    /// </summary>
    /// <param name="audioClip">対象AudioClip</param>
    /// <param name="startSample">開始サンプル位置</param>
    /// <param name="chunkDurationMs">チャンク長さ(ms)</param>
    /// <returns>チャンクのPCM16データ</returns>
    public static byte[] ExtractChunkFromAudioClip(AudioClip audioClip, int startSample, float chunkDurationMs = 50f)
    {
        if (audioClip == null)
        {
            Debug.LogWarning("AudioConverter: AudioClipがnullです");
            return new byte[0];
        }
        
        int sampleRate = audioClip.frequency;
        int samplesPerChunk = Mathf.RoundToInt(sampleRate * (chunkDurationMs / 1000f));
        
        // 範囲チェック
        if (startSample + samplesPerChunk > audioClip.samples)
        {
            samplesPerChunk = audioClip.samples - startSample;
        }
        
        if (samplesPerChunk <= 0)
        {
            return new byte[0];
        }
        
        // AudioClipからサンプルデータ取得
        float[] samples = new float[samplesPerChunk * audioClip.channels];
        audioClip.GetData(samples, startSample);
        
        // PCM16に変換
        return ConvertToPCM16(samples);
    }
    
    /// <summary>
    /// 音声データの品質情報を取得
    /// </summary>
    /// <param name="audioData">PCM16音声データ</param>
    /// <param name="sampleRate">サンプリングレート</param>
    /// <returns>音声情報文字列</returns>
    public static string GetAudioInfo(byte[] audioData, int sampleRate = 24000)
    {
        if (audioData == null || audioData.Length == 0)
            return "音声データなし";
        
        int sampleCount = audioData.Length / 2;
        float durationSeconds = (float)sampleCount / sampleRate;
        
        return $"サンプル数: {sampleCount}, 長さ: {durationSeconds:F2}秒, サイズ: {audioData.Length}bytes";
    }
}
