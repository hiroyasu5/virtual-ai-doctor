using UnityEngine;
using Cysharp.Threading.Tasks;  // �� UniTask ���g���錾

public class UniTaskTest : MonoBehaviour
{
    // �Q�[���J�n���ɌĂ΂��
    private async UniTaskVoid Start()
    {
        Debug.Log("Start ����");

        // 1 �b�i1000 ms�j�����҂�
        await UniTask.Delay(1000);

        Debug.Log("1 �b�o�� �� UniTask OK!");
    }
}

