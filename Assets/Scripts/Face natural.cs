using UnityEngine;
using System.Collections.Generic;

public class ExpressionController : MonoBehaviour
{
    public SkinnedMeshRenderer faceRenderer;

    private float idleTimer = 0f;
    private float blinkTimer = 0f;
    private float blinkInterval = 3f;

    private bool isTalking = false;
    private Dictionary<string, int> shapeIndices = new Dictionary<string, int>();

    void Start()
    {
        AddBlendShape("Fcl_ALL_Fun");
        AddBlendShape("Fcl_ALL_Angry");
        AddBlendShape("Fcl_EYE_Close_L");
        AddBlendShape("Fcl_EYE_Close_R");
    }

    void Update()
    {
        if (!isTalking)
        {
            idleTimer += Time.deltaTime;
            if (idleTimer > 10f)
            {
                SetExpression("Fcl_ALL_Angry");
            }
            else
            {
                SetExpression("Fcl_ALL_Fun");
            }
        }

        blinkTimer += Time.deltaTime;
        if (blinkTimer > blinkInterval)
        {
            Debug.Log("Blink trigger!");
            StartCoroutine(Blink());
            blinkTimer = 0f;
            blinkInterval = Random.Range(2f, 5f);
        }
    }

    public void OnStartTalking()
    {
        isTalking = true;
        idleTimer = 0f;
        ResetAll();
    }

    public void OnStopTalking()
    {
        isTalking = false;
    }

    void SetExpression(string shapeName)
    {
        foreach (var pair in shapeIndices)
        {
            float weight = 0f;

            if (pair.Key == shapeName)
            {
                if (shapeName == "Fcl_ALL_Fun") weight = 50f;
                else if (shapeName == "Fcl_ALL_Angry") weight = 100f;
            }

            faceRenderer.SetBlendShapeWeight(pair.Value, weight);
        }
    }

    System.Collections.IEnumerator Blink()
    {
        bool hasLeft = shapeIndices.ContainsKey("Fcl_EYE_Close_L");
        bool hasRight = shapeIndices.ContainsKey("Fcl_EYE_Close_R");

        if (hasLeft && hasRight)
        {
            Debug.Log("瞬き開始");
            faceRenderer.SetBlendShapeWeight(shapeIndices["Fcl_EYE_Close_L"], 100f);
            faceRenderer.SetBlendShapeWeight(shapeIndices["Fcl_EYE_Close_R"], 100f);
            yield return new WaitForSeconds(0.1f);
            faceRenderer.SetBlendShapeWeight(shapeIndices["Fcl_EYE_Close_L"], 0f);
            faceRenderer.SetBlendShapeWeight(shapeIndices["Fcl_EYE_Close_R"], 0f);
            Debug.Log("瞬き終了");
        }
        else
        {
            Debug.LogWarning("瞬き用のBlendShapeが見つかりませんでした（Fcl_EYE_Close_L / R）");
        }
    }

    void AddBlendShape(string name)
    {
        int index = faceRenderer.sharedMesh.GetBlendShapeIndex(name);
        if (index >= 0)
        {
            shapeIndices[name] = index;
        }
        else
        {
            Debug.LogWarning($"BlendShape '{name}' が見つかりません");
        }
    }

    void ResetAll()
    {
        foreach (var pair in shapeIndices)
        {
            faceRenderer.SetBlendShapeWeight(pair.Value, 0f);
        }
    }
}
