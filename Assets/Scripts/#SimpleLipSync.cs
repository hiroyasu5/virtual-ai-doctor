using UnityEngine;
using System.Collections.Generic;

public class SimpleLipSync : MonoBehaviour
{
    public AudioSource audioSource;
    public SkinnedMeshRenderer faceRenderer;

    public List<string> blendShapeNames = new List<string> {
        "Fcl_MTH_A", "Fcl_MTH_I", "Fcl_MTH_U", "Fcl_MTH_E", "Fcl_MTH_O"
    };

    private Dictionary<string, int> blendShapeIndices = new Dictionary<string, int>();
    private float switchInterval = 0.1f; // 母音切り替え間隔（秒）
    private float timer = 0f;
    private string currentVowel = "Fcl_MTH_A";

    void Start()
    {
        foreach (var name in blendShapeNames)
        {
            int index = faceRenderer.sharedMesh.GetBlendShapeIndex(name);
            if (index >= 0)
            {
                blendShapeIndices[name] = index;
            }
            else
            {
                Debug.LogWarning($"BlendShape {name} が見つかりませんでした");
            }
        }
    }

    void Update()
    {
        if (audioSource.isPlaying)
        {
            timer += Time.deltaTime;
            if (timer >= switchInterval)
            {
                timer = 0f;
                int random = Random.Range(0, blendShapeNames.Count);
                currentVowel = blendShapeNames[random];
            }

            float loudness = GetAveragedVolume() * 1000f;
            loudness = Mathf.Clamp(loudness, 0f, 100f);

            foreach (var kvp in blendShapeIndices)
            {
                float weight = kvp.Key == currentVowel ? loudness : 0f;
                faceRenderer.SetBlendShapeWeight(kvp.Value, weight);
            }
        }
        else
        {
            foreach (var kvp in blendShapeIndices)
            {
                faceRenderer.SetBlendShapeWeight(kvp.Value, 0f);
            }
        }
    }

    float GetAveragedVolume()
    {
        float[] data = new float[256];
        float sum = 0;
        audioSource.GetOutputData(data, 0);
        foreach (float s in data)
        {
            sum += Mathf.Abs(s);
        }
        return sum / 256;
    }
}
