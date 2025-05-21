# 1) ベースイメージは公式 Voicevox エンジン
FROM voicevox/voicevox_engine:latest

# 2) Render がくれる $PORT をそのまま開放
EXPOSE $PORT

# 3) シェル経由で uvicorn を呼び出す
#    └ ベースイメージには uvicorn が install されている想定
CMD ["bash", "-lc", "uvicorn --host 0.0.0.0 --port $PORT voicevox_engine.app:app"]


