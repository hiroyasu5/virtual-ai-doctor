using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TalkButtonController : MonoBehaviour
{
    public Button talkButton;
    public TextMeshProUGUI statusText;
    private bool isTalking = false;

    void Start()
    {
        talkButton.onClick.AddListener(StartConversation);
    }

    void StartConversation()
    {
        isTalking = true;
        talkButton.gameObject.SetActive(false);
        statusText.text = "TALKING MODE";

        // 仮の処理を動かす
        StartCoroutine(FakeConversationLoop());
    }

    System.Collections.IEnumerator FakeConversationLoop()
    {
        while (isTalking)
        {
            Debug.Log("話しかけを待っています...");
            yield return new WaitForSeconds(2f); // 今は仮に2秒ごとに処理

            Debug.Log("返答中...");
            yield return new WaitForSeconds(1f);

            Debug.Log("再び聞き取り中...");
        }
    }
}
