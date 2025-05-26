import os
import dotenv

# プロジェクトルートの .env を自動検出
dotenv.load_dotenv(dotenv.find_dotenv())

OPENAI_WSS = os.getenv("OPENAI_REALTIME_ENDPOINT")
OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")
HEADERS = {"Authorization": f"Bearer {OPENAI_API_KEY}"}

# 修正: より適切な条件チェック
if not OPENAI_WSS:
    raise RuntimeError("OPENAI_REALTIME_ENDPOINT 環境変数が設定されていません")

if not OPENAI_API_KEY or not OPENAI_API_KEY.startswith("sk-"):
    raise RuntimeError("OPENAI_API_KEY 環境変数が正しく設定されていません")

print(f"✅ 環境変数チェック完了")
print(f"📍 エンドポイント: {OPENAI_WSS}")
print(f"🔑 APIキー: {OPENAI_API_KEY[:7]}..." if OPENAI_API_KEY else "未設定")
