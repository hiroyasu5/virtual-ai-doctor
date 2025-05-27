# mcp/app/routers/realtime.py
import asyncio
import json
import logging
from fastapi import APIRouter, WebSocket, WebSocketDisconnect
import websockets
from websockets.exceptions import ConnectionClosed, WebSocketException
from ..core.config import OPENAI_WSS, OPENAI_API_KEY, SESSION_CONFIG  # OPENAI_API_KEY直接インポート
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
        # 🔧 修正: extra_headers を additional_headers に変更
        additional_headers = {
            "Authorization": f"Bearer {OPENAI_API_KEY}"
        }
        
        async with websockets.connect(
            OPENAI_WSS,
            additional_headers=additional_headers,  # 修正: extra_headers → additional_headers
            ping_interval=20,
            ping_timeout=10
        ) as openai_ws:
            logger.info("✅ Connected to OpenAI Realtime API")
            
            # SESSION_CONFIG送信
            session_update = {
                "type": "session.update",
                "session": SESSION_CONFIG
            }
            await openai_ws.send(json.dumps(session_update))
            logger.info("✅ PCM16出力形式を設定完了")
            
            async def client_to_openai():
                """クライアント → OpenAI方向の音声データ転送"""
                try:
                    async for pcm_data in ws.iter_bytes():
                        await openai_ws.send(pcm_data)
                        logger.info(f"🎤 音声送信: {len(pcm_data)} bytes")  # INFOレベルに変更
                except WebSocketDisconnect:
                    logger.info("Client WebSocket disconnected")
                except Exception as e:
                    logger.error(f"Error in client_to_openai: {e}")
                    raise
            
            async def openai_to_client():
                """OpenAI → クライアント方向の応答転送"""
                try:
                    async for message in openai_ws:
                        if isinstance(message, bytes):
                            # PCM16データをそのまま転送
                            await ws.send_bytes(message)
                            logger.info(f"🔊 PCM16音声転送: {len(message)} bytes")
                        else:
                            # JSONメッセージの処理
                            try:
                                data = json.loads(message)
                                
                                # Session確認応答
                                if data.get("type") == "session.updated":
                                    logger.info("✅ OpenAI session configuration confirmed")
                                
                                # Function call の処理
                                if "function_call" in data:
                                    await handle_function(data["function_call"])
                                
                                # デバッグ用: transcript のログ出力
                                if "transcript" in data and data["transcript"]:
                                    logger.info(f"📝 Transcript: {data['transcript']}")
                                
                                # その他のイベント
                                event_type = data.get("type", "unknown")
                                logger.debug(f"📡 OpenAI Event: {event_type}")
                                
                            except json.JSONDecodeError as e:
                                logger.warning(f"Invalid JSON from OpenAI: {e}")
                                
                except (ConnectionClosed, WebSocketException) as e:
                    logger.info(f"OpenAI WebSocket connection closed: {e}")
                except Exception as e:
                    logger.error(f"Error in openai_to_client: {e}")
                    raise
            
            # 双方向通信を並列実行
            await asyncio.gather(
                client_to_openai(),
                openai_to_client(),
                return_exceptions=True
            )
            
    except WebSocketDisconnect:
        logger.info("Client disconnected")
    except Exception as e:
        logger.error(f"Unexpected error in relay: {e}")
        try:
            await ws.close(code=1011, reason="Internal server error")
        except:
            pass
    finally:
        logger.info(f"WebSocket session ended: {id(ws)}")

@router.get("/health")
async def health_check():
    """サーバーの稼働状況確認"""
    return {
        "status": "healthy", 
        "service": "realtime-audio-relay",
        "audio_format": "pcm16_direct",
        "voice_model": SESSION_CONFIG["voice"],
        "websockets_fixed": True  # 修正完了フラグ
    }
