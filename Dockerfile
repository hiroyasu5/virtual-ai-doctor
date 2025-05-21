# ---- base image -------------------------------------------------
# 公式 VOICEVOX Engine （CPU 版）イメージ
FROM voicevox/voicevox_engine:latest

# ---- network port ----------------------------------------------
# Render が “PORT” 環境変数を注入してくるのでそのまま公開
EXPOSE $PORT

# ---- startup ----------------------------------------------------
# 公式イメージには Python / uvicorn / voicevox_engine が
# すべて入っているので、そのままモジュール実行するだけ
CMD ["bash", "-lc", "python -m voicevox_engine --host 0.0.0.0 --port $PORT"]



