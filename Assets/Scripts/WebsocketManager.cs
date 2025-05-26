using System;
using System.Threading.Tasks;
using NativeWebSocket;
using UnityEngine;

/// <summary>
/// WebSocket接続を管理するクラス
/// FastAPIサーバーとのリアルタイム音声通信を担当
/// </summary>
public class WebSocketManager : MonoBehaviour
{
    [Header("接続設定")]
    [SerializeField] private string serverURL = "ws://127.0.0.1:8000/ws/audio";
    [SerializeField] private bool autoConnect = true;
    [SerializeField] private float reconnectInterval = 5f;
    
    [Header("デバッグ")]
    [SerializeField] private bool showDebugLog = true;
    
    // WebSocket関連
    private WebSocket websocket;
    private bool isConnecting = false;
    private bool shouldReconnect = true;
    
    // プロパティ
    public bool IsConnected { get; private set; } = false;
    public string ServerURL => serverURL;
    
    // イベント
    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<byte[]> OnAudioReceived;
    public event Action<string> OnError;
    
    #region Unity Lifecycle
    
    private void Start()
    {
        if (autoConnect)
        {
            ConnectToServer();
        }
    }
    
    private void Update()
    {
        // WebSocketメッセージキューを処理
        websocket?.DispatchMessageQueue();
    }
    
    private async void OnDestroy()
    {
        shouldReconnect = false;
        await DisconnectFromServer();
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // アプリがバックグラウンドに移行時
            LogDebug("WebSocketManager: アプリケーション一時停止");
        }
        else
        {
            // アプリがフォアグラウンドに復帰時
            LogDebug("WebSocketManager: アプリケーション復帰");
            if (!IsConnected && shouldReconnect)
            {
                ConnectToServer();
            }
        }
    }
    
    #endregion
    
    #region 接続管理
    
    /// <summary>
    /// サーバーに接続
    /// </summary>
    public async void ConnectToServer()
    {
        if (isConnecting || IsConnected)
        {
            LogDebug("WebSocketManager: すでに接続中または接続済みです");
            return;
        }
        
        try
        {
            isConnecting = true;
            LogDebug($"WebSocketManager: サーバーに接続中... ({serverURL})");
            
            // 既存の接続がある場合はクリーンアップ
            if (websocket != null)
            {
                await websocket.Close();
                websocket = null;
            }
            
            // 新しいWebSocket接続を作成
            websocket = new WebSocket(serverURL);
            
            // イベントハンドラー設定
            SetupWebSocketEvents();
            
            // 接続実行
            await websocket.Connect();
        }
        catch (Exception e)
        {
            LogError($"WebSocketManager: 接続エラー - {e.Message}");
            isConnecting = false;
            OnError?.Invoke(e.Message);
            
            // 再接続スケジュール
            if (shouldReconnect)
            {
                ScheduleReconnect();
            }
        }
    }
    
    /// <summary>
    /// サーバーから切断
    /// </summary>
    public async Task DisconnectFromServer()
    {
        shouldReconnect = false;
        
        if (websocket != null)
        {
            try
            {
                LogDebug("WebSocketManager: サーバーから切断中...");
                await websocket.Close();
            }
            catch (Exception e)
            {
                LogError($"WebSocketManager: 切断エラー - {e.Message}");
            }
            finally
            {
                websocket = null;
                IsConnected = false;
                isConnecting = false;
            }
        }
    }
    
    /// <summary>
    /// WebSocketイベントハンドラーの設定
    /// </summary>
    private void SetupWebSocketEvents()
    {
        websocket.OnOpen += () =>
        {
            LogDebug("WebSocketManager: 接続成功！");
            IsConnected = true;
            isConnecting = false;
            OnConnected?.Invoke();
        };
        
        websocket.OnError += (errorMessage) =>
        {
            LogError($"WebSocketManager: WebSocketエラー - {errorMessage}");
            OnError?.Invoke(errorMessage);
        };
        
        websocket.OnClose += (closeCode) =>
        {
            LogDebug($"WebSocketManager: 接続終了 (Code: {closeCode})");
            IsConnected = false;
            isConnecting = false;
            OnDisconnected?.Invoke();
            
            // 予期しない切断の場合は再接続を試行
            if (shouldReconnect && closeCode != WebSocketCloseCode.Normal)
            {
                ScheduleReconnect();
            }
        };
        
        websocket.OnMessage += (bytes) =>
        {
            try
            {
                LogDebug($"WebSocketManager: 音声データ受信 ({bytes.Length} bytes)");
                OnAudioReceived?.Invoke(bytes);
            }
            catch (Exception e)
            {
                LogError($"WebSocketManager: メッセージ処理エラー - {e.Message}");
            }
        };
    }
    
    /// <summary>
    /// 再接続をスケジュール
    /// </summary>
    private void ScheduleReconnect()
    {
        if (!shouldReconnect) return;
        
        LogDebug($"WebSocketManager: {reconnectInterval}秒後に再接続を試行");
        Invoke(nameof(ConnectToServer), reconnectInterval);
    }
    
    #endregion
    
    #region データ送信
    
    /// <summary>
    /// 音声データを送信
    /// </summary>
    /// <param name="audioData">PCM16音声データ</param>
    public async void SendAudioData(byte[] audioData)
    {
        if (!IsConnected)
        {
            LogDebug("WebSocketManager: 未接続のため音声データを送信できません");
            return;
        }
        
        if (audioData == null || audioData.Length == 0)
        {
            LogDebug("WebSocketManager: 空の音声データは送信しません");
            return;
        }
        
        try
        {
            await websocket.Send(audioData);
            LogDebug($"WebSocketManager: 音声データ送信完了 ({audioData.Length} bytes)");
        }
        catch (Exception e)
        {
            LogError($"WebSocketManager: 音声データ送信エラー - {e.Message}");
        }
    }
    
    /// <summary>
    /// テキストメッセージを送信（デバッグ用）
    /// </summary>
    /// <param name="message">送信メッセージ</param>
    public async void SendTextMessage(string message)
    {
        if (!IsConnected)
        {
            LogDebug("WebSocketManager: 未接続のためテキストを送信できません");
            return;
        }
        
        try
        {
            await websocket.SendText(message);
            LogDebug($"WebSocketManager: テキスト送信完了 - {message}");
        }
        catch (Exception e)
        {
            LogError($"WebSocketManager: テキスト送信エラー - {e.Message}");
        }
    }
    
    #endregion
    
    #region 公開メソッド
    
    /// <summary>
    /// 接続状態を文字列で取得
    /// </summary>
    /// <returns>接続状態文字列</returns>
    public string GetConnectionStatus()
    {
        if (IsConnected)
            return "接続中";
        else if (isConnecting)
            return "接続試行中";
        else
            return "切断";
    }
    
    /// <summary>
    /// 手動再接続
    /// </summary>
    public void ManualReconnect()
    {
        shouldReconnect = true;
        ConnectToServer();
    }
    
    /// <summary>
    /// サーバーURLを変更
    /// </summary>
    /// <param name="newURL">新しいサーバーURL</param>
    public async void ChangeServerURL(string newURL)
    {
        if (string.IsNullOrEmpty(newURL))
        {
            LogError("WebSocketManager: 無効なサーバーURLです");
            return;
        }
        
        serverURL = newURL;
        LogDebug($"WebSocketManager: サーバーURL変更 - {serverURL}");
        
        // 接続中の場合は再接続
        if (IsConnected)
        {
            await DisconnectFromServer();
            shouldReconnect = true;
            ConnectToServer();
        }
    }
    
    #endregion
    
    #region デバッグ
    
    /// <summary>
    /// デバッグログ出力
    /// </summary>
    /// <param name="message">ログメッセージ</param>
    private void LogDebug(string message)
    {
        if (showDebugLog)
        {
            Debug.Log(message);
        }
    }
    
    /// <summary>
    /// エラーログ出力
    /// </summary>
    /// <param name="message">エラーメッセージ</param>
    private void LogError(string message)
    {
        Debug.LogError(message);
    }
    
    #endregion
}