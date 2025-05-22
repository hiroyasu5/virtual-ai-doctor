# ---- base image -------------------------------------------------
FROM voicevox/voicevox_engine:latest

# ---- network port ----------------------------------------------
EXPOSE 50021

# ---- startup ----------------------------------------------------
ENV PORT=50021
CMD ["sh", "-c", "python -m voicevox_engine --host 0.0.0.0 --port ${PORT}"]




