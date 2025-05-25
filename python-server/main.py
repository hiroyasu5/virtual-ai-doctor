# python-server/main.py
import os, json
from fastapi import FastAPI, WebSocket, WebSocketDisconnect
from openai import OpenAI
import redis.asyncio as aioredis
from dotenv import load_dotenv

load_dotenv("../.env")  # ルートの .env を読み込む

app = FastAPI()
client = OpenAI(api_key=os.getenv("OPENAI_API_KEY"))
r = aioredis.from_url(os.getenv("REDIS_URL", "redis://redis:6379/0"))

@app.websocket("/ws/realtime")
async def websocket_realtime(ws: WebSocket):
    """届いた PCM バイト列をそのまま送り返すだけのエコー"""
    await ws.accept()
    cid = str(id(ws))
    await r.hset("realtime_sessions", cid, "open")
    try:
        while True:
            pcm = await ws.receive_bytes()   # ブロックして受信
            await ws.send_bytes(pcm)         # そのまま返す
    except WebSocketDisconnect:
        pass
    finally:
        await r.hdel("realtime_sessions", cid)

# ---------------------------------------------------------------------------
# ★OpenAI 呼び出しはコメントアウトして保管しておけばすぐ戻せます★
