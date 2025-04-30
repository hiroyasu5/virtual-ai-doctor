using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WavingHand : MonoBehaviour
{
    [Header("Joints")]
    [SerializeField] private Transform elbowJoint;
    [SerializeField] private Transform wristJoint;

    [Header("Initial Rotation (Elbow)")]
    [SerializeField] private Vector3 elbowInitialRotation = new Vector3(40f, 100f, 277.055023f);

    [Header("Wrist Initial Rotation")]
    [SerializeField] private Vector3 wristInitialRotation = new Vector3(0f, 0f, 0f);

    [Header("Wave Settings")]
    [SerializeField] private float waveAmplitude = 10f;   // 振れ幅（X軸回転）
    [SerializeField] private float waveSpeed = 2f;        // 振るスピード

    private float waveTime = 0f;

    void Start()
    {
        // 初期姿勢に固定
        if (elbowJoint != null)
            elbowJoint.localRotation = Quaternion.Euler(elbowInitialRotation);

        if (wristJoint != null)
            wristJoint.localRotation = Quaternion.Euler(wristInitialRotation);
    }

    void Update()
    {
        waveTime += Time.deltaTime * waveSpeed;

        if (elbowJoint != null)
        {
            // X軸にsin波で揺らす
            float angleOffsetX = Mathf.Sin(waveTime) * waveAmplitude;

            Vector3 newRotation = elbowInitialRotation + new Vector3(angleOffsetX, 0f, 0f);
            elbowJoint.localRotation = Quaternion.Euler(newRotation);
        }
    }
}
