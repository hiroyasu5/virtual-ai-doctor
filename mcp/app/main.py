# mcp/app/main.py
import logging
from fastapi import FastAPI
from .routers import realtime  # 相対インポートに変更

# ログレベルの設定（INFO以上を出力）
logging.basicConfig(level=logging.INFO)

# FastAPI アプリ起動
app = FastAPI()

# ルーター登録
app.include_router(realtime.router)
