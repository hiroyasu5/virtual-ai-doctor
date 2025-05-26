# mcp/app/routers/realtime.py
import asyncio
import json
import logging
from fastapi import APIRouter, WebSocket, WebSocketDisconnect
import websockets
from websockets.exceptions import ConnectionClosed, WebSocketException
from ..core.config import OPENAI_WSS, HEADERS  # 相対インポートに変更
from ..services.function import handle_function  # 相対インポートに変更

# ログ設定
logger = logging.getLogger(__name__)
router = APIRouter()

@router.websocket("/ws/audio")
async def relay(ws: WebSocket):
    """リアルタイム音声通信の中継エンドポイント"""
    await ws.accept()
    logger.info(f"WebSocket connection established: {id(ws)}")
    
    try:
        async with websockets.connect(
            OPENAI_WSS, 
            extra_headers=HEADERS,
            ping_interval=20,  # 接続維持
            ping_timeout=10
        ) as openai_ws:
            logger.info("Connected to OpenAI Realtime API")
            
            async def client_to_openai():
                """クライアント → OpenAI方向の音声データ転送"""
                try:
                    async for pcm_data in ws.iter_bytes():
                        await openai_ws.send(pcm_data)
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
                            # GPT-4oからの音声データ
                            await ws.send_bytes(message)
                        else:
                            # JSONメッセージの処理
                            try:
                                data = json.loads(message)
                                
                                # Function call の処理
                                if "function_call" in data:
                                    await handle_function(data["function_call"])
                                
                                # デバッグ用: transcript のログ出力
                                if "transcript" in data and data["transcript"]:
                                    logger.info(f"Transcript: {data['transcript']}")
                                
                                # 必要に応じてクライアントにもJSONを転送
                                # await ws.send_text(message)
                                
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
                return_exceptions=True  # 一方の例外で全体を停止させない
            )
            
    except WebSocketDisconnect:
        logger.info("Client disconnected")
    except (ConnectionClosed, WebSocketException) as e:
        logger.warning(f"WebSocket connection error: {e}")
    except Exception as e:
        logger.error(f"Unexpected error in relay: {e}")
        # クライアントが接続中なら適切にクローズ
        try:
            await ws.close(code=1011, reason="Internal server error")
        except:
            pass
    finally:
        logger.info(f"WebSocket session ended: {id(ws)}")

# 追加: ヘルスチェック用エンドポイント
@router.get("/health")
async def health_check():
    """サーバーの稼働状況確認"""
    return {"status": "healthy", "service": "realtime-audio-relay"}
