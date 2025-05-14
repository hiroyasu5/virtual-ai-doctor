using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using System.IO;

public class VoicevoxTTS : MonoBehaviour
{
    public string speakerId = "14"; // 冥鳴ひまり
    public AudioSource audioSource;

    public void Speak(string text)
    {
        StartCoroutine(SynthesizeAndPlay(text));
    }

    IEnumerator SynthesizeAndPlay(string text)
    {
        // Step1: 音声クエリ生成
        string queryUrl = $"http://127.0.0.1:50021/audio_query?text={UnityWebRequest.EscapeURL(text)}&speaker={speakerId}";
        UnityWebRequest queryReq = UnityWebRequest.PostWwwForm(queryUrl, "");
        yield return queryReq.SendWebRequest();
        if (queryReq.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Query failed: " + queryReq.error);
            yield break;
        }

        // Step2: 音声合成
        byte[] queryJson = Encoding.UTF8.GetBytes(queryReq.downloadHandler.text);
        UnityWebRequest synthReq = new UnityWebRequest($"http://127.0.0.1:50021/synthesis?speaker={speakerId}", "POST");
        synthReq.uploadHandler = new UploadHandlerRaw(queryJson);
        synthReq.downloadHandler = new DownloadHandlerBuffer();
        synthReq.SetRequestHeader("Content-Type", "application/json");
        synthReq.SetRequestHeader("Accept", "audio/wav");

        yield return synthReq.SendWebRequest();
        if (synthReq.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Synthesis failed: " + synthReq.error);
            yield break;
        }

        // Step3: .wavを保存して再生
        string tempPath = Path.Combine(Application.persistentDataPath, "voicevox.wav");
        File.WriteAllBytes(tempPath, synthReq.downloadHandler.data);

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + tempPath, AudioType.WAV))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                audioSource.clip = clip;
                audioSource.Play();
            }
            else
            {
                Debug.LogError("Audio load failed: " + www.error);
            }
        }
    }
}