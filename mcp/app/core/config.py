# mcp/app/core/config.py - 方式B対応版
import os
import dotenv

# プロジェクトルートの .env を自動検出
dotenv.load_dotenv(dotenv.find_dotenv())

OPENAI_WSS = "wss://api.openai.com/v1/realtime"
OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")

# ✅ 公式ドキュメントに従った正確なモデル名
AVAILABLE_MODELS = [
    "gpt-4o-mini-realtime-preview",        # 公式ドキュメント通り
    "gpt-4o-realtime-preview", 
]

MODEL_NAME = os.getenv("OPENAI_MODEL", AVAILABLE_MODELS[0])

# ✅ websockets 11.0.3 対応：辞書形式に変更
HEADERS = {
    "Authorization": f"Bearer {OPENAI_API_KEY}",
    "OpenAI-Beta": "realtime=v1"
}

# 環境変数チェック
if not OPENAI_API_KEY or not OPENAI_API_KEY.startswith("sk-"):
    raise RuntimeError("OPENAI_API_KEY 環境変数が正しく設定されていません")

# ✅ SESSION_CONFIG - 方式B対応
SESSION_CONFIG = {
    "modalities": ["text", "audio"],        # ✅ OpenAI要求: 両方必須
    "voice": "alloy",
    "model": MODEL_NAME,
    "input_audio_format": "pcm16",
    "output_audio_format": "pcm16",
    # ⭐ 方式B: 手動制御のための設定
    "turn_detection": {
        "type": "server_vad",               # サーバー側で音声検出
        "threshold": 0.5,                   # 音声検出の感度
        "prefix_padding_ms": 300,           # 音声開始前のパディング
        "silence_duration_ms": 500,         # 沈黙検出時間
        "create_response": False            # ⭐ 自動応答生成を無効化（方式Bの要）
    }
}

# ✅ WebSocket URL生成関数
def get_websocket_url():
    return f"{OPENAI_WSS}?model={MODEL_NAME}"

print(f"✅ 環境変数チェック完了")
print(f"📍 エンドポイント: {get_websocket_url()}")
print(f"🔑 APIキー: {OPENAI_API_KEY[:10]}..." if OPENAI_API_KEY else "未設定")
print(f"🤖 モデル: {MODEL_NAME}")
print(f"🎵 音声フォーマット: PCM16, 16kHz")
print(f"📊 期待チャンクサイズ: 1,600 bytes（16kHz×50ms×2bytes）")
print(f"🎙️ 音声検出: サーバー側VAD有効（手動制御モード）")


