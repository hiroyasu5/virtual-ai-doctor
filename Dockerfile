# voicevox-engine/Dockerfile  ←ファイル名（必ず半角スペースなし）

FROM voicevox/voicevox_engine:latest  # 公式イメージをそのまま使う
EXPOSE $PORT                          # Render が渡してくる PORT を公開
CMD ["bash","-lc","python -m voicevox_engine --host 0.0.0.0 --port $PORT"]




