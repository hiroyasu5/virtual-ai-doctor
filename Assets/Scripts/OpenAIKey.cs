using UnityEngine;
using System.IO;

public static class OpenAIKey
{
    public static string Value;

    // ゲーム開始時に一度だけ呼び出す
    [RuntimeInitializeOnLoadMethod]
    static void Load()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "openai_key.txt");

        if (File.Exists(path))
        {
            Value = File.ReadAllText(path).Trim();
            Debug.Log($"API Key 前半: {Value.Substring(0, 8)}...");
        }
        else
        {
            Debug.LogError($"openai_key.txt が見つかりません: {path}");
        }
    }
}


