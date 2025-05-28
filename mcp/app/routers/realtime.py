import websockets, sys, pathlib, logging
logging.getLogger(__name__).warning(
    f"⚙️  websockets {websockets.__version__} @ {pathlib.Path(websockets.__file__).parent}"
)

# mcp/app/routers/realtime.py
import asyncio
import json
import logging
from fastapi import APIRouter, WebSocket, WebSocketDisconnect
import websockets
from websockets.exceptions import ConnectionClosed, WebSocketException
from ..core.config import OPENAI_WSS, OPENAI_API_KEY, SESSION_CONFIG  # OPENAI_API_KEY 直接インポート
from ..services.function import handle_function

# ログ設定
logger = logging.getLogger(__name__)
router = APIRouter()

@router.websocket("/ws/audio")
async def relay(ws: WebSocket):
    """リアルタイム音声通信の中継エンドポイント"""
    await ws.accept()
    logger.info(f"WebSocket connection established: {id(ws)}")

    try:
        # ---- 正しい形式は tuple list で "OpenAI-Beta" ヘッダーを付与 ----
        additional_headers = [
            ("Authorization", f"Bearer {OPENAI_API_KEY}"),
            ("OpenAI-Beta", "realtime=v1"),
        ]

        async with websockets.connect(
            OPENAI_WSS,
            extra_headers=additional_headers,
            ping_interval=20,
            ping_timeout=10,
        ) as openai_ws:
            logger.info("✅ Connected to OpenAI Realtime API")

            # SESSION_CONFIG 送信
            await openai_ws.send(
                json.dumps({
                    "type": "session.update",
                    "session": SESSION_CONFIG,
                })
            )
            logger.info("✅ PCM16 出力形式を設定完了")

            async def client_to_openai():
                try:
                    async for pcm_data in ws.iter_bytes():
                        await openai_ws.send(pcm_data)
                        logger.debug(f"🎤 Sent {len(pcm_data)} B to OpenAI")
                except WebSocketDisconnect:
                    logger.info("Client disconnected")
                except Exception as exc:
                    logger.error(f"client_to_openai error: {exc}")
                    raise

            async def openai_to_client():
                try:
                    async for message in openai_ws:
                        if isinstance(message, bytes):
                            await ws.send_bytes(message)
                            logger.debug(f"🔊 Forwarded {len(message)} B to client")
                        else:
                            try:
                                data = json.loads(message)
                                if data.get("type") == "session.updated":
                                    logger.info("✅ OpenAI session confirmed")
                                if "function_call" in data:
                                    await handle_function(data["function_call"])
                                if data.get("transcript"):
                                    logger.info(f"📝 Transcript: {data['transcript']}")
                            except json.JSONDecodeError as exc:
                                logger.warning(f"Invalid JSON from OpenAI: {exc}")
                except (ConnectionClosed, WebSocketException) as exc:
                    logger.info(f"OpenAI WS closed: {exc}")
                except Exception as exc:
                    logger.error(f"openai_to_client error: {exc}")
                    raise

            await asyncio.gather(
                client_to_openai(),
                openai_to_client(),
                return_exceptions=True,
            )

    except WebSocketDisconnect:
        logger.info("Client WS disconnected")
    except Exception as exc:
        logger.error(f"Unexpected relay error: {exc}")
        try:
            await ws.close(code=1011, reason="Internal server error")
        except Exception:
            pass
    finally:
        logger.info(f"WebSocket session ended: {id(ws)}")


@router.get("/health")
async def health_check():
    """サーバー稼働状況確認エンドポイント"""
    return {
        "status": "healthy",
        "service": "realtime-audio-relay",
        "audio_format": SESSION_CONFIG["output_audio_format"],
        "voice_model": SESSION_CONFIG["voice"],
        "model": SESSION_CONFIG["model"],
    }

