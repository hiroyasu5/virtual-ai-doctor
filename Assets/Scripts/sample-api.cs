using UnityEngine;

public class EnvTest : MonoBehaviour
{
    void Start()
    {
        string apiKey = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Debug.Log($"API Key �O��: {apiKey?.Substring(0,8)}..."); // �����Ɠǂ߂����m�F
    }
}

