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

with open("functions.json") as f:
    FUNCTIONS = json.load(f)

@app.websocket("/ws/realtime")
async def websocket_realtime(ws: WebSocket):
    await ws.accept()
    cid = str(id(ws))
    await r.hset("realtime_sessions", cid, "open")
    try:
        while True:
            chunk = await ws.receive_bytes()
            # OpenAI Realtime API 呼び出し（プレビュー的実装）
            stream = client.audio.speech.create(
                model="gpt-4o-mini-tts",
                input=chunk,
                stream=True,
            )
            async for frame in stream:
                await ws.send_bytes(frame)
    except WebSocketDisconnect:
        pass
    finally:
        await r.hdel("realtime_sessions", cid)
