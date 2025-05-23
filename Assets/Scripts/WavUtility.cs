using UnityEngine;
using System;

/// <summary>
/// WAV → AudioClip / AudioClip → PCM バイト列 の相互変換ユーティリティ
/// </summary>
public static class WavUtility
{
    /// <summary>
    /// float[] のサンプルデータを PCM16bit のバイト列に変換します。
    /// </summary>
    public static byte[] FromAudioClipSegment(float[] samples, int sampleRate)
    {
        short[] intData = new short[samples.Length];
        byte[] bytesData = new byte[samples.Length * 2];
        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)(samples[i] * short.MaxValue);
            var byteArr = BitConverter.GetBytes(intData[i]);
            bytesData[i * 2]     = byteArr[0];
            bytesData[i * 2 + 1] = byteArr[1];
        }
        return bytesData;
    }

    /// <summary>
    /// PCM16bit のバイト列（ヘッダなし）から AudioClip を生成します。
    /// </summary>
    public static AudioClip ToAudioClip(byte[] pcmBytes, int channels, string clipName)
    {
        int sampleCount = pcmBytes.Length / 2;
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short val = BitConverter.ToInt16(pcmBytes, i * 2);
            samples[i] = val / (float)short.MaxValue;
        }
        var clip = AudioClip.Create(clipName, sampleCount / channels, channels, 16000, false);
        clip.SetData(samples, 0);
        return clip;
    }

    /// <summary>
    /// AudioClip 全体を PCM16bit のバイト列に変換します。
    /// </summary>
    /// <param name="clip">変換元の AudioClip</param>
    /// <returns>PCM16bit リトルエンディアンのバイト配列</returns>
    public static byte[] FromAudioClip(AudioClip clip)
    {
        if (clip == null) return new byte[0];

        // クリップの全サンプルを読み出し
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        // サンプリングレートは clip.frequency から取得
        return FromAudioClipSegment(samples, clip.frequency);
    }
}
