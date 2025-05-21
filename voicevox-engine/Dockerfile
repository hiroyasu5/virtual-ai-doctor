# voicevox-engine/Dockerfile
FROM voicevox/voicevox_engine:latest          # ① 公式イメージ

EXPOSE $PORT                                  # ② Render が渡すポートを公開

CMD ["bash","-lc","python -m voicevox_engine --host 0.0.0.0 --port $PORT"]  # ③ 起動



