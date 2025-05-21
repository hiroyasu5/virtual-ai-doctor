# voicevox-engine/Dockerfile
FROM voicevox/voicevox_engine:latest

# Render が渡してくる PORT 環境変数をそのまま使う
# （デフォルト 50021 も同じだから、冗長に ENGINE_PORT を作らなくても大丈夫）
EXPOSE $PORT

# 最終行でこのコンテナを起動。必ず $PORT で待ち受ける
CMD ["bash","-lc","uvicorn --host 0.0.0.0 --port $PORT voicevox_engine.app:app"]

