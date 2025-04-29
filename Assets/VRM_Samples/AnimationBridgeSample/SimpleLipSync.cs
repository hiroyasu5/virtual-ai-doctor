using UnityEngine;

public class SimpleLipSync : MonoBehaviour
{
    public AudioSource audioSource;
    public SkinnedMeshRenderer faceRenderer;  // ���������d�v�I�I�i���Ԃ񔲂��Ă�j

    public string blendShapeName = "Fcl_MTH_A"; // ���̊J��BlendShape�̖��O

    private int blendShapeIndex;

    void Start()
    {
        // "A"�Ƃ������O��BlendShape�̔ԍ����擾
        blendShapeIndex = faceRenderer.sharedMesh.GetBlendShapeIndex(blendShapeName);
    }

    void Update()
    {
        if (audioSource.isPlaying)
        {
            if (blendShapeIndex >= 0)  // �������ǉ�
            {
                float loudness = GetAveragedVolume() * 1000f; // ���ʂ𑝕�
                loudness = Mathf.Clamp(loudness, 0f, 100f);   // 0?100�ɐ���
                faceRenderer.SetBlendShapeWeight(blendShapeIndex, loudness);
            }
        }
        else
        {
            if (blendShapeIndex >= 0)  // �������ǉ�
            {
                faceRenderer.SetBlendShapeWeight(blendShapeIndex, 0f); // �������~�܂�����������
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
