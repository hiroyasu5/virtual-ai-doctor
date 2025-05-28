# mcp/app/core/config.py - websockets 15.0.1 対応版
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

# ✅ websockets 15.0.1 対応：辞書形式に変更
HEADERS = {
    "Authorization": f"Bearer {OPENAI_API_KEY}",
    "OpenAI-Beta": "realtime=v1"
}

# 環境変数チェック
if not OPENAI_API_KEY or not OPENAI_API_KEY.startswith("sk-"):
    raise RuntimeError("OPENAI_API_KEY 環境変数が正しく設定されていません")

# ✅ SESSION_CONFIG - OpenAI公式仕様準拠
SESSION_CONFIG = {
    "modalities": ["text", "audio"],        # ✅ OpenAI要求: 両方必須
    "voice": "alloy",
    "model": MODEL_NAME,
    "input_audio_format": "pcm16",
    "output_audio_format": "pcm16"
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
