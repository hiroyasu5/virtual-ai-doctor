# mcp/app/routers/realtime.py - 方式B最終修正版（音声重複問題解決）

"""Unity ⇆ OpenAI Realtime Audio API - 方式B（手動制御＋バージイン）
----------------------------------------------------------------
修正内容:
1. 音声重複の防止: response_in_progressフラグで厳密に管理
2. 空バッファエラーの解消: 無音検出を改善
3. バージイン改善: 応答キャンセルのタイミング最適化
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
            ping_interval=None,
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

        # 🌟 共有状態
        assistant_speaking = asyncio.Event()
        response_in_progress = asyncio.Event()  # 応答生成中フラグ

        # --------------------------- start proxy tasks -----------------------
        await asyncio.gather(
            _unity_to_openai(ws, openai_ws, assistant_speaking, response_in_progress),
            _openai_to_unity(ws, openai_ws, assistant_speaking, response_in_progress),
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
# Task 1: Unity → OpenAI（音声重複防止版）
# -----------------------------------------------------------------------------

async def _unity_to_openai(
    unity_ws: WebSocket, 
    openai_ws: websockets.WebSocketClientProtocol,
    assistant_speaking: asyncio.Event,
    response_in_progress: asyncio.Event
) -> None:
    """PCM16 chunks → base64 & append/commit with duplicate prevention"""
    idx = 0
    audio_buffer_size = 0
    has_voice = False  # 実際の音声があるか
    
    async for pcm in unity_ws.iter_bytes():
        if not pcm:
            continue
        
        # 音声レベルチェック（無音検出）
        import struct
        try:
            samples = struct.unpack(f"{len(pcm)//2}h", pcm)
            max_amplitude = max(abs(s) for s in samples) if samples else 0
            # より高い閾値で無音を判定
            if max_amplitude > 500:  # 閾値を上げる
                has_voice = True
                logger.debug(f"🎤 音声検出: 振幅 {max_amplitude}")
        except:
            pass
        
        # バージイン処理
        if idx == 0 and assistant_speaking.is_set():
            await openai_ws.send(json.dumps({"type": "response.cancel"}))
            logger.info("🛑 User interrupted - cancelling AI response")
            assistant_speaking.clear()
            response_in_progress.clear()
        
        # 音声データを追加
        await openai_ws.send(json.dumps({
            "type": "input_audio_buffer.append",
            "audio": base64.b64encode(pcm).decode(),
        }))
        
        idx += 1
        audio_buffer_size += len(pcm)
        
        # 20チャンク（約1秒）ごとに処理
        if idx >= 20:
            logger.info(f"📊 音声バッファ: {audio_buffer_size} bytes, 音声あり: {has_voice}")
            
            # 音声がある場合のみコミット
            if has_voice and audio_buffer_size > 3200:  # 2チャンク分以上
                await openai_ws.send(json.dumps({"type": "input_audio_buffer.commit"}))
                
                # 応答生成中でない場合のみリクエスト
                if not response_in_progress.is_set() and not assistant_speaking.is_set():
                    response_in_progress.set()  # フラグを立てる
                    await openai_ws.send(json.dumps({
                        "type": "response.create",
                        "response": {
                            "modalities": ["audio", "text"],
                            "instructions": "あなたは親切な医療アシスタントです。簡潔に応答してください。",
                            "voice": "alloy",
                            "temperature": 0.7,
                        },
                    }))
                    logger.info("📤 応答リクエスト送信")
                else:
                    logger.info("📤 コミットのみ（応答生成中）")
            else:
                # 無音の場合はバッファをクリア
                await openai_ws.send(json.dumps({"type": "input_audio_buffer.clear"}))
                logger.info("🔇 無音のためバッファクリア")
            
            # リセット
            idx = 0
            audio_buffer_size = 0
            has_voice = False

# -----------------------------------------------------------------------------
# Task 2: OpenAI → Unity（応答管理改善版）
# -----------------------------------------------------------------------------

async def _openai_to_unity(
    unity_ws: WebSocket, 
    openai_ws: websockets.WebSocketClientProtocol,
    assistant_speaking: asyncio.Event,
    response_in_progress: asyncio.Event
) -> None:
    """OpenAI events → Unity with improved response management"""
    
    async for m in openai_ws:
        if isinstance(m, str):
            try:
                d: dict[str, Any] = json.loads(m)
            except json.JSONDecodeError:
                continue
                
            t = d.get("type", "?")
            
            # 音声データの転送
            if t == "response.audio.delta":
                delta = d.get("delta", "")
                if delta:
                    try:
                        audio_bytes = base64.b64decode(delta)
                        await unity_ws.send_bytes(audio_bytes)
                        # 初回の音声データで話し始めを記録
                        if not assistant_speaking.is_set():
                            assistant_speaking.set()
                            logger.info("🔊 Assistant started speaking")
                            logger.info(f"📤 最初の音声データ送信: {len(audio_bytes)} bytes")
                    except Exception as e:
                        logger.error(f"❌ 音声データ送信エラー: {e}")
                        
            # 応答完了
            elif t == "response.done":
                assistant_speaking.clear()
                response_in_progress.clear()  # フラグをクリア
                logger.info("✅ Assistant finished speaking")
                
            # 応答キャンセル完了
            elif t == "response.cancelled":
                assistant_speaking.clear()
                response_in_progress.clear()
                logger.info("❌ Response cancelled")
                
            # 音声認識結果
            elif t == "conversation.item.input_audio_transcription.completed":
                transcript = d.get("transcript", "")
                if transcript:
                    logger.info(f"📝 User said: {transcript}")
                    
            # AI応答のテキスト
            elif t == "response.audio_transcript.delta":
                transcript = d.get("delta", "")
                if transcript:
                    logger.info(f"🤖 AI: {transcript}")
                    
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
                error_code = d.get("error", {}).get("code", "")
                # 空バッファエラーは無視
                if error_code != "input_audio_buffer_commit_empty":
                    logger.error(f"❌ OpenAI error: {d}")
                
            # デバッグ用
            else:
                if t not in ["session.created", "session.updated", "response.created"]:
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
        "mode": "manual_control_v2",
        "create_response": "false",
        "barge_in": "enabled",
        "duplicate_prevention": "enabled",
        "url": get_websocket_url(),
    }
