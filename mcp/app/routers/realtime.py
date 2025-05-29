# mcp/app/routers/realtime.py - 方式B完全実装版（バージイン対応）

"""Unity ⇆ OpenAI Realtime Audio API - 方式B（手動制御＋バージイン）
----------------------------------------------------------------
特徴:
1. create_response: False で自動応答生成を無効化
2. 20チャンク（約1秒）ごとに手動でcommit + response.create
3. バージイン対応: ユーザーが話し始めたらAIの応答をキャンセル
4. 応答の重複を完全に防止
"""

from __future__ import annotations

import asyncio
import base64
import json
import logging
from typing import Any

from fastapi import APIRouter, WebSocket, WebSocketDisconnect
import websockets
from websockets.exceptions import ConnectionClosed, WebSocketException, InvalidStatusCode

from ..core.config import (
    get_websocket_url,
    OPENAI_API_KEY,
    SESSION_CONFIG,
    MODEL_NAME,
)
from ..services.function import handle_function

logger = logging.getLogger(__name__)
router = APIRouter()

# -----------------------------------------------------------------------------
# WebSocket relay
# -----------------------------------------------------------------------------

@router.websocket("/ws/audio")
async def relay(ws: WebSocket) -> None:  # noqa: C901
    """Unity ↔ FastAPI ↔ OpenAI realtime audio relay"""
    await ws.accept()
    logger.info("Unity WS connected: %s", id(ws))

    url = get_websocket_url()
    extra_headers = [
        ("Authorization", f"Bearer {OPENAI_API_KEY}"),
        ("OpenAI-Beta", "realtime=v1"),
    ]

    openai_ws: websockets.WebSocketClientProtocol | None = None

    try:
        # --------------------------- connect to OpenAI ------------------------
        openai_ws = await websockets.connect(
            url,
            extra_headers=extra_headers,
            ping_interval=None,   # 🔕 disable websocket‑level ping
            ping_timeout=None,
            close_timeout=10,
        )
        logger.info("✅ OpenAI WS connected (ping disabled)")

        # -- wait session.created ---------------------------------------------
        created = await _expect_json(openai_ws, "session.created", 10)
        if created is None:
            await _abort(ws, "session.created timeout")
            return

        # -- send session.update ----------------------------------------------
        await openai_ws.send(json.dumps({
            "type": "session.update",
            "session": SESSION_CONFIG,
        }))
        logger.info("📤 session.update sent (create_response: False)")

        # wait for session.updated
        updated = await _expect_json(openai_ws, "session.updated", 5)
        if updated:
            logger.info("✅ session.updated received")

        # 🌟 共有状態: 助手が話しているかどうか
        assistant_speaking = asyncio.Event()

        # --------------------------- start proxy tasks -----------------------
        await asyncio.gather(
            _unity_to_openai(ws, openai_ws, assistant_speaking),
            _openai_to_unity(ws, openai_ws, assistant_speaking),
            return_exceptions=True,
        )

    except WebSocketDisconnect:
        logger.info("Unity disconnected")
    except InvalidStatusCode as exc:
        await _abort(ws, f"OpenAI WS status {exc.status_code}")
    except Exception as exc:  # noqa: BLE001
        logger.exception("relay fatal: %s", exc)
        await _abort(ws, "internal error")
    finally:
        if openai_ws is not None:
            await _safe_close(openai_ws)
        logger.info("session ended: %s", id(ws))

# -----------------------------------------------------------------------------
# Task 1: Unity → OpenAI（バージイン対応）
# -----------------------------------------------------------------------------

async def _unity_to_openai(
    unity_ws: WebSocket, 
    openai_ws: websockets.WebSocketClientProtocol,
    assistant_speaking: asyncio.Event
) -> None:
    """PCM16 chunks → base64 & append/commit with barge-in support"""
    idx = 0
    user_speaking = False
    has_active_response = False  # 応答生成中フラグ
    audio_buffer_size = 0  # バッファサイズ追跡
    
    async for pcm in unity_ws.iter_bytes():
        if not pcm:
            continue
        
        # 音声レベルチェック（デバッグ用）
        if idx == 0:
            import struct
            try:
                samples = struct.unpack(f"{len(pcm)//2}h", pcm)
                max_amplitude = max(abs(s) for s in samples) if samples else 0
                if max_amplitude > 100:  # 閾値
                    logger.debug(f"🎤 音声検出: 振幅 {max_amplitude}")
            except:
                pass
        
        # 🌟 バージイン処理: ユーザーが話し始めた瞬間
        if idx == 0 and assistant_speaking.is_set():
            # AIが話している最中なら中断
            await openai_ws.send(json.dumps({"type": "response.cancel"}))
            logger.info("🛑 User interrupted - cancelling AI response")
            assistant_speaking.clear()
            has_active_response = False
        
        idx += 1
        audio_buffer_size += len(pcm)
        
        # 音声データを送信
        await openai_ws.send(json.dumps({
            "type": "input_audio_buffer.append",
            "audio": base64.b64encode(pcm).decode(),
        }))
        
        # 20チャンク（約1秒）ごとにコミット＆応答生成
        if idx >= 20 and not has_active_response:
            # バッファサイズチェック
            logger.info(f"📊 音声バッファサイズ: {audio_buffer_size} bytes ({audio_buffer_size/32000:.2f}秒)")
            
            await openai_ws.send(json.dumps({"type": "input_audio_buffer.commit"}))
            
            # 既に応答生成中でない場合のみリクエスト
            if not assistant_speaking.is_set():
                await openai_ws.send(json.dumps({
                    "type": "response.create",
                    "response": {
                        "modalities": ["audio", "text"],
                        "instructions": "あなたは親切な医療アシスタントです。簡潔に応答してください。",
                        "voice": "alloy",  # 明示的に音声指定
                        "temperature": 0.7,
                    },
                }))
                has_active_response = True
                logger.info(f"📤 Committed {idx} chunks & requested response")
            else:
                logger.info(f"📤 Committed {idx} chunks (response already active)")
            
            idx = 0
            audio_buffer_size = 0

# -----------------------------------------------------------------------------
# Task 2: OpenAI → Unity
# -----------------------------------------------------------------------------

async def _openai_to_unity(
    unity_ws: WebSocket, 
    openai_ws: websockets.WebSocketClientProtocol,
    assistant_speaking: asyncio.Event
) -> None:
    """OpenAI events → Unity with speaking state tracking"""
    
    async for m in openai_ws:
        if isinstance(m, str):
            try:
                d: dict[str, Any] = json.loads(m)
            except json.JSONDecodeError:
                continue
                
            t = d.get("type", "?")
            
            # 音声データの転送
            if t == "response.audio.delta":
                b64 = d.get("delta", "")
                if b64:
                    await unity_ws.send_bytes(base64.b64decode(b64))
                    # 音声再生開始を記録
                    if not assistant_speaking.is_set():
                        assistant_speaking.set()
                        logger.info("🔊 Assistant started speaking")
                        
            # 応答完了
            elif t == "response.done":
                assistant_speaking.clear()
                logger.info("✅ Assistant finished speaking")
                
            # 音声認識結果
            elif t == "conversation.item.input_audio_transcription.completed":
                transcript = d.get("transcript", "")
                if transcript:
                    logger.info(f"📝 User said: {transcript}")
                    
            # AI応答のテキスト
            elif t == "response.audio_transcript.delta":
                transcript = d.get("delta", "")
                if transcript:
                    logger.info(f"🤖 AI response: {transcript}")
                    
            # 音声検出イベント
            elif t == "input_audio_buffer.speech_started":
                logger.debug("🎙️ Speech detected")
            elif t == "input_audio_buffer.speech_stopped":
                logger.debug("🎙️ Speech ended")
                
            # Function calling
            elif t == "response.function_call_arguments.done":
                await handle_function(d)
                
            # エラー
            elif t.startswith("error"):
                logger.error(f"❌ OpenAI error: {d}")
                
            # デバッグ用
            else:
                logger.debug(f"📨 OpenAI event: {t}")

# -----------------------------------------------------------------------------
# Utilities
# -----------------------------------------------------------------------------

async def _expect_json(ws: websockets.WebSocketClientProtocol, typ: str, timeout: float):
    try:
        raw = await asyncio.wait_for(ws.recv(), timeout)
        if isinstance(raw, str):
            data = json.loads(raw)
            if data.get("type") == typ:
                return data
    except asyncio.TimeoutError:
        pass
    return None

async def _abort(unity_ws: WebSocket, reason: str):
    logger.error("abort: %s", reason)
    try:
        await unity_ws.close(code=1011, reason=reason)
    except Exception:
        pass

async def _safe_close(ws: websockets.WebSocketClientProtocol):
    try:
        await ws.close()
    except Exception:
        pass

# -----------------------------------------------------------------------------

@router.get("/health")
async def health():
    return {
        "status": "healthy",
        "model": MODEL_NAME,
        "mode": "manual_control",
        "create_response": "false",
        "barge_in": "enabled",
        "url": get_websocket_url(),
    }

