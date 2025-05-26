using UnityEngine;
using UnityEngine.UI;

public class VoiceChatController : MonoBehaviour
{
    [Header("Components")]
    public WebSocketManager webSocketManager;
    public AudioManager audioManager;
    public Button micButton;
    public Text statusText;
    
    [Header("Avatar")]
    public Animator avatarAnimator; // VRoidアバターのAnimator
    
    private bool isTalking = false;
    
    private void Start()
    {
        // イベント登録
        webSocketManager.OnConnected += OnWebSocketConnected;
        webSocketManager.OnDisconnected += OnWebSocketDisconnected;
        webSocketManager.OnAudioReceived += OnAudioReceived;
        audioManager.OnAudioChunkReady += OnAudioChunkReady;
        
        // UI設定
        micButton.onClick.AddListener(ToggleMicrophone);
        UpdateUI();
    }
    
    private void OnWebSocketConnected()
    {
        statusText.text = "接続完了";
        statusText.color = Color.green;
        micButton.interactable = true;
    }
    
    private void OnWebSocketDisconnected()
    {
        statusText.text = "接続切断";
        statusText.color = Color.red;
        micButton.interactable = false;
    }
    
    private void OnAudioReceived(byte[] audioData)
    {
        // OpenAIからの音声再生
        audioManager.PlayReceivedAudio(audioData);
        
        // アバターのアニメーション
        StartAvatarTalking();
    }
    
    private void OnAudioChunkReady(byte[] audioData)
    {
        // サーバーに音声データ送信
        webSocketManager.SendAudioData(audioData);
    }
    
    private void ToggleMicrophone()
    {
        if (!isTalking)
        {
            audioManager.StartRecording();
            isTalking = true;
            micButton.GetComponentInChildren<Text>().text = "録音停止";
        }
        else
        {
            audioManager.StopRecording();
            isTalking = false;
            micButton.GetComponentInChildren<Text>().text = "録音開始";
        }
    }
    
    private void StartAvatarTalking()
    {
        // VRoidアバターの口の動きアニメーション
        if (avatarAnimator != null)
        {
            avatarAnimator.SetBool("IsTalking", true);
            // 音声長に応じて停止タイマー設定
            Invoke("StopAvatarTalking", 3f);
        }
    }
    
    private void StopAvatarTalking()
    {
        if (avatarAnimator != null)
        {
            avatarAnimator.SetBool("IsTalking", false);
        }
    }
    
    private void UpdateUI()
    {
        bool connected = webSocketManager.IsConnected;
        micButton.interactable = connected;
        statusText.text = connected ? "接続完了" : "接続中...";
        statusText.color = connected ? Color.green : Color.yellow;
    }
}

