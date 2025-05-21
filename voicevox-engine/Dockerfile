# voicevox-engine/Dockerfile
FROM voicevox/voicevox_engine:latest

# Render が割り当ててくれるポートを環境変数 ENGINE_PORT にセット
ENV ENGINE_PORT $PORT

# そのポートを公開
EXPOSE ${ENGINE_PORT}

# ★ CMD を意図的に書かない（公式イメージの ENTRYPOINT が動きます）

