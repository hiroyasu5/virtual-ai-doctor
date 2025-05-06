using UnityEngine;
using Cysharp.Threading.Tasks;  // ← UniTask を使う宣言

public class UniTaskTest : MonoBehaviour
{
    // ゲーム開始時に呼ばれる
    private async UniTaskVoid Start()
    {
        Debug.Log("Start 直後");

        // 1 秒（1000 ms）だけ待つ
        await UniTask.Delay(1000);

        Debug.Log("1 秒経過 → UniTask OK!");
    }
}

