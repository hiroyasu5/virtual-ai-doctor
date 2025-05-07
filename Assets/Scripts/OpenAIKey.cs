using UnityEngine;
using System.IO;

public static class OpenAIKey
{
    public static string Value;

    // �Q�[���J�n���Ɉ�x�����Ăяo��
    [RuntimeInitializeOnLoadMethod]
    static void Load()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "openai_key.txt");

        if (File.Exists(path))
        {
            Value = File.ReadAllText(path).Trim();
            Debug.Log($"API Key �O��: {Value.Substring(0, 8)}...");
        }
        else
        {
            Debug.LogError($"openai_key.txt ��������܂���: {path}");
        }
    }
}


