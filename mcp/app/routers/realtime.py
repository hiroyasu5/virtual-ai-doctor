# mcp/app/routers/realtime.py - websockets 15.0.1 対応版
import asyncio
import json
import logging
from fastapi import APIRouter, WebSocket, WebSocketDisconnect
import websockets
from websockets.exceptions import ConnectionClosed, WebSocketException
from ..core.config import get_websocket_url, HEADERS, SESSION_CONFIG, MODEL_NAME, OPENAI_API_KEY
from ..services.function import handle_function

# ログ設定
logger = logging.getLogger(__name__)
router = APIRouter()

@router.websocket("/ws/audio")
async def relay(ws: WebSocket):
    """リアルタイム音声通信の中継エンドポイント"""
    await ws.accept()
    logger.info(f"WebSocket connection established: {id(ws)}")
    
    openai_ws = None
    
    try:
        # ✅ websockets 15.0.1 対応：ヘッダー形式修正
        url = get_websocket_url()
        logger.info(f"🔗 OpenAI接続先: {url}")
        logger.info(f"🔑 APIキー: {OPENAI_API_KEY[:15]}...")
        
        # ✅ websockets 11.0.3 対応
        extra_headers = [
            ("Authorization", f"Bearer {OPENAI_API_KEY}"),
            ("OpenAI-Beta", "realtime=v1"),
        ]
        
        # WebSocket接続の試行（シンプル設定）
        openai_ws = await websockets.connect(url, extra_headers=extra_headers)
        logger.info("✅ OpenAI WebSocket 接続成功")
        
        # ✅ 初期メッセージを待つ（session.created）
        try:
            initial_message = await asyncio.wait_for(openai_ws.recv(), timeout=10.0)
            if isinstance(initial_message, str):
                data = json.loads(initial_message)
                logger.info(f"📨 初期メッセージ: {data.get('type', 'unknown')}")
                
                if data.get('type') == 'session.created':
                    logger.info("🎉 session.created 受信成功！")
                elif data.get('type') == 'error':
                    error_msg = data.get('error', {}).get('message', 'Unknown')
                    logger.error(f"❌ OpenAI エラー: {error_msg}")
                    await ws.close(code=1011, reason=f"OpenAI Error: {error_msg}")
                    return
            else:
                logger.warning(f"⚠️ 予期しない初期メッセージ形式: {type(initial_message)}")
        
        except asyncio.TimeoutError:
            logger.error("❌ session.created タイムアウト")
            await ws.close(code=1011, reason="OpenAI session timeout")
            return
        
        # ✅ SESSION_CONFIG を送信
        session_update = {
            "type": "session.update",
            "session": SESSION_CONFIG
        }
        await openai_ws.send(json.dumps(session_update))
        logger.info(f"📤 SESSION_CONFIG 送信: {SESSION_CONFIG}")
        
        # ✅ session.updated を待つ
        try:
            update_response = await asyncio.wait_for(openai_ws.recv(), timeout=5.0)
            if isinstance(update_response, str):
                data = json.loads(update_response)
                if data.get('type') == 'session.updated':
                    logger.info("✅ session.updated 受信 - 設定完了")
                elif data.get('type') == 'error':
                    error_msg = data.get('error', {}).get('message', 'Unknown')
                    logger.error(f"❌ SESSION_CONFIG エラー: {error_msg}")
                    await ws.close(code=1011, reason="Session config error")
                    return
        except asyncio.TimeoutError:
            logger.warning("⚠️ session.updated タイムアウト（継続）")
        
        # ✅ 双方向通信開始
        async def client_to_openai():
            """Unity → OpenAI"""
            try:
                async for pcm_data in ws.iter_bytes():
                    if len(pcm_data) > 0:
                        await openai_ws.send(pcm_data)
                        logger.debug(f"🎤 Unity→OpenAI: {len(pcm_data)} bytes")
            except WebSocketDisconnect:
                logger.info("Unity クライアント切断")
            except Exception as exc:
                logger.error(f"client_to_openai エラー: {exc}")
                raise
        
        async def openai_to_client():
            """OpenAI → Unity"""
            try:
                async for message in openai_ws:
                    if isinstance(message, bytes):
                        # 音声データを Unity に転送
                        await ws.send_bytes(message)
                        logger.debug(f"🔊 OpenAI→Unity: {len(message)} bytes")
                    else:
                        # JSON メッセージの処理
                        try:
                            data = json.loads(message)
                            msg_type = data.get("type", "unknown")
                            
                            if msg_type == "response.audio.delta":
                                pass  # 音声チャンクは既に上で処理
                            elif msg_type == "conversation.item.input_audio_transcription.completed":
                                logger.info(f"📝 音声認識: {data.get('transcript', '')}")
                            elif msg_type == "response.done":
                                logger.info("✅ 応答完了")
                            elif msg_type == "error":
                                logger.error(f"❌ OpenAI エラー: {data.get('error', {})}")
                            elif "function_call" in data:
                                await handle_function(data["function_call"])
                            else:
                                logger.debug(f"📨 OpenAI メッセージ: {msg_type}")
                        
                        except json.JSONDecodeError as exc:
                            logger.warning(f"⚠️ JSON解析エラー: {exc}")
            
            except (ConnectionClosed, WebSocketException) as exc:
                logger.info(f"OpenAI WebSocket 切断: {exc}")
            except Exception as exc:
                logger.error(f"openai_to_client エラー: {exc}")
                raise
        
        # 並行処理開始
        await asyncio.gather(
            client_to_openai(),
            openai_to_client(),
            return_exceptions=True,
        )
    
    except WebSocketDisconnect:
        logger.info("Unity WebSocket 切断")
    except websockets.exceptions.InvalidStatusCode as exc:
        logger.error(f"❌ OpenAI接続エラー (Status: {exc.status_code})")
        try:
            await ws.close(code=1011, reason=f"OpenAI connection failed: {exc.status_code}")
        except Exception:
            pass
    except Exception as exc:
        logger.error(f"❌ 予期しないエラー: {exc}")
        try:
            await ws.close(code=1011, reason="Internal server error")
        except Exception:
            pass
    finally:
        if openai_ws:
            try:
                await openai_ws.close()
            except Exception:
                pass
        logger.info(f"WebSocket セッション終了: {id(ws)}")


@router.get("/health")
async def health_check():
    """サーバー稼働状況確認エンドポイント"""
    return {
        "status": "healthy",
        "service": "realtime-audio-relay",
        "websocket_url": get_websocket_url(),
        "model": MODEL_NAME,
        "audio_format": SESSION_CONFIG["output_audio_format"],
        "modalities": SESSION_CONFIG["modalities"],
    }
