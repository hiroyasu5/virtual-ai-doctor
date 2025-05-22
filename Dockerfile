FROM voicevox/voicevox_engine:latest

RUN ln -s /usr/bin/python3 /usr/bin/python

EXPOSE 50021

ENV PORT=50021
CMD ["sh", "-c", "python -m voicevox_engine --host 0.0.0.0 --port ${PORT}"]






