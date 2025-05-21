# voicevox-engine/Dockerfile
# 公式の Voicevox Engine イメージをベースに使う
FROM voicevox/voicevox_engine:latest

# Render 上では環境変数 $PORT に空きポートが渡されるので
# Uvicorn をこのポートで起動する
ENV ENGINE_PORT=${PORT:-50021}

EXPOSE ${ENGINE_PORT}

# サーバーを起動。app モジュール名はエンジン公式構成
CMD ["bash", "-lc", "uvicorn --host 0.0.0.0 --port $ENGINE_PORT voicevox_engine.app:app"]

