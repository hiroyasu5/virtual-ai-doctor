using UnityEngine;

public class EnvTest : MonoBehaviour
{
    void Start()
    {
        string apiKey = System.Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        Debug.Log($"API Key ‘O”¼: {apiKey?.Substring(0,8)}..."); // ‚¿‚á‚ñ‚Æ“Ç‚ß‚½‚©Šm”F
    }
}

