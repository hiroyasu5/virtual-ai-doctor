using UnityEngine;

public class EnvTest : MonoBehaviour
{
    void Start()
    {
        string apiKey = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Debug.Log($"API Key 前半: {apiKey?.Substring(0,8)}..."); // ちゃんと読めたか確認
    }
}

