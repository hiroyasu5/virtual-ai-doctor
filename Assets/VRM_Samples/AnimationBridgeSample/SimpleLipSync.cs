using UnityEngine;

public class SimpleLipSync : MonoBehaviour
{
    public AudioSource audioSource;
    public SkinnedMeshRenderer faceRenderer;  // ←ここが重要！！（たぶん抜けてる）

    public string blendShapeName = "Fcl_MTH_A"; // 口の開きBlendShapeの名前

    private int blendShapeIndex;

    void Start()
    {
        // "A"という名前のBlendShapeの番号を取得
        blendShapeIndex = faceRenderer.sharedMesh.GetBlendShapeIndex(blendShapeName);
    }

    void Update()
    {
        if (audioSource.isPlaying)
        {
            if (blendShapeIndex >= 0)  // ★ここ追加
            {
                float loudness = GetAveragedVolume() * 1000f; // 音量を増幅
                loudness = Mathf.Clamp(loudness, 0f, 100f);   // 0?100に制限
                faceRenderer.SetBlendShapeWeight(blendShapeIndex, loudness);
            }
        }
        else
        {
            if (blendShapeIndex >= 0)  // ★ここ追加
            {
                faceRenderer.SetBlendShapeWeight(blendShapeIndex, 0f); // 音声が止まったら口を閉じる
            }
        }
    }


    float GetAveragedVolume()
    {
        float[] data = new float[256];
        float a = 0;
        audioSource.GetOutputData(data, 0);
        foreach (float s in data)
        {
            a += Mathf.Abs(s);
        }
        return a / 256;
    }
}
