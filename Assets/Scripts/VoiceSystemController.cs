using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 音声システム全体制御（新しい名前で競合回避）
/// </summary>
public class VoiceSystemController : MonoBehaviour
{
    [Header("必須コンポーネント")]
    [SerializeField] private WebSocketManager webSocketManager;
    [SerializeField] private AudioManager audioManager;
    
    [Header("UI要素")]
    [SerializeField] private Button micButton;
    [SerializeField] private Text statusText;
    
    [Header("アバター")]
    [SerializeField] private Animator avatarAnimator;
    
    private bool isTalking = false;
    private bool isConnected = false;
    
    void Start()
    {
        Debug.Log("VoiceSystemController 開始");
        SetupEvents();
        UpdateUI();
    }
    
    void SetupEvents()
    {
        // WebSocketイベント
        if (webSocketManager != null)
        {
            webSocketManager.OnConnected += OnConnected;
            webSocketManager.OnDisconnected += OnDisconnected;
            webSocketManager.OnAudioReceived += OnAudioReceived;
        }
        
        // Audioイベント
        if (audioManager != null)
        {
            audioManager.OnAudioChunkReady += OnAudioChunkReady;
        }
        
        // UIイベント
        if (micButton != null)
        {
            micButton.onClick.AddListener(ToggleMic);
        }
    }
    
    void OnConnected()
    {
        Debug.Log("WebSocket接続成功");
        isConnected = true;
        UpdateUI();
    }
    
    void OnDisconnected()
    {
        Debug.Log("WebSocket切断");
        isConnected = false;
        if (isTalking)
        {
            StopTalking();
        }
        UpdateUI();
    }
    
    void OnAudioReceived(byte[] audioData)
    {
        Debug.Log($"音声受信: {audioData.Length} bytes");
        
        // 音声再生
        if (audioManager != null)
        {
            audioManager.PlayReceivedAudio(audioData);
        }
        
        // アバターアニメーション
        StartAvatarTalking();
    }
    
    void OnAudioChunkReady(byte[] audioData)
    {
        // 音声データ送信
        if (webSocketManager != null && isConnected)
        {
            webSocketManager.SendAudioData(audioData);
        }
    }
    
    void ToggleMic()
    {
        if (!isConnected)
        {
            Debug.Log("未接続のためマイク操作不可");
            return;
        }
        
        if (isTalking)
        {
            StopTalking();
        }
        else
        {
            StartTalking();
        }
    }
    
    void StartTalking()
    {
        if (audioManager != null)
        {
            audioManager.StartRecording();
            isTalking = true;
            Debug.Log("録音開始");
        }
        UpdateUI();
    }
    
    void StopTalking()
    {
        if (audioManager != null)
        {
            audioManager.StopRecording();
            isTalking = false;
            Debug.Log("録音停止");
        }
        UpdateUI();
    }
    
    void StartAvatarTalking()
    {
        if (avatarAnimator != null)
        {
            avatarAnimator.SetBool("IsTalking", true);
            Invoke("StopAvatarTalking", 3f);
        }
    }
    
    void StopAvatarTalking()
    {
        if (avatarAnimator != null)
        {
            avatarAnimator.SetBool("IsTalking", false);
        }
    }
    
    void UpdateUI()
    {
        if (statusText != null)
        {
            if (isConnected)
            {
                statusText.text = isTalking ? "録音中" : "待機中";
                statusText.color = isTalking ? Color.red : Color.green;
            }
            else
            {
                statusText.text = "接続中...";
                statusText.color = Color.yellow;
            }
        }
        
        if (micButton != null)
        {
            micButton.interactable = isConnected;
            Text buttonText = micButton.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = isTalking ? "停止" : "開始";
            }
        }
    }
    
    void OnDestroy()
    {
        // イベント解除
        if (webSocketManager != null)
        {
            webSocketManager.OnConnected -= OnConnected;
            webSocketManager.OnDisconnected -= OnDisconnected;
            webSocketManager.OnAudioReceived -= OnAudioReceived;
        }
        
        if (audioManager != null)
        {
            audioManager.OnAudioChunkReady -= OnAudioChunkReady;
        }
    }

    // 一時的なテスト用 - VoiceSystemController に追加

[Header("デバッグ用")]
[SerializeField] private bool manualConnectionTest = false;

void Update()
{
    // 手動接続テスト（Spaceキー）
    if (manualConnectionTest && Input.GetKeyDown(KeyCode.Space))
    {
        if (webSocketManager != null)
        {
            if (webSocketManager.IsConnected)
            {
                Debug.Log("手動切断実行");
                _ = webSocketManager.DisconnectFromServer();
            }
            else
            {
                Debug.Log("手動接続実行");
                webSocketManager.ConnectToServer();
            }
        }
    }
    
    // 接続状態モニタリング
    if (manualConnectionTest && webSocketManager != null)
    {
        string status = webSocketManager.GetConnectionStatus();
        if (statusText != null && statusText.text != status)
        {
            Debug.Log($"接続状態変化: {status}");
        }
    }
}
}