import os
import dotenv

# プロジェクトルートの .env を自動検出
dotenv.load_dotenv(dotenv.find_dotenv())

OPENAI_WSS = os.getenv("OPENAI_REALTIME_ENDPOINT")
OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")
HEADERS = {"Authorization": f"Bearer {OPENAI_API_KEY}"}

# 環境変数チェック
if not OPENAI_WSS:
    raise RuntimeError("OPENAI_REALTIME_ENDPOINT 環境変数が設定されていません")

if not OPENAI_API_KEY or not OPENAI_API_KEY.startswith("sk-"):
    raise RuntimeError("OPENAI_API_KEY 環境変数が正しく設定されていません")

# 🎯 修正：OpenAI Realtime API正しい仕様（16kHz）
SESSION_CONFIG = {
    "modalities": ["text", "audio"],
    "voice": "alloy",  # 医療用途に最適
    "model": "gpt-4o-mini-realtime-preview",
    "input_audio_format": "pcm16",
    "output_audio_format": "pcm16"
    # 注意：サンプリングレートは16kHzが標準
}

print(f"✅ 環境変数チェック完了")
print(f"📍 エンドポイント: {OPENAI_WSS}")
print(f"🔑 APIキー: {OPENAI_API_KEY[:7]}..." if OPENAI_API_KEY else "未設定")
print(f"🎵 音声フォーマット: PCM16, 16kHz（OpenAI標準仕様）")
print(f"📊 期待チャンクサイズ: 1,600 bytes（16kHz×50ms×2bytes）")
