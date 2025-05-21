# ← プロジェクトルート直下に置く Dockerfile

# 公式 Voicevox Engine イメージをベースに使う
FROM voicevox/voicevox_engine:latest

# Render が渡してくる PORT 環境変数をそのまま開放
EXPOSE $PORT

# Python3 モジュールとして Uvicorn を起動。必ず $PORT で待ち受ける
CMD ["bash", "-lc", "python3 -m uvicorn --host 0.0.0.0 --port $PORT voicevox_engine.app:app"]


