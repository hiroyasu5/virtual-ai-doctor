 # voicevox-engine/Dockerfile
 FROM voicevox/voicevox_engine:latest

-# Render から渡されるドア番号(PORT)をそのまま開ける
-EXPOSE $PORT
-
-# uvicorn を直接呼ぼうとすると見つからないケースがあるので、
-# Python3 にモジュールとして実行させる
-CMD ["bash","-lc","uvicorn --host 0.0.0.0 --port $PORT voicevox_engine.app:app"]
+## Render から渡されるドア番号(PORT)をそのまま開ける
+EXPOSE $PORT
+
+# Python3 でモジュールとして uvicorn を起動させる
+CMD ["bash","-lc","python3 -m uvicorn --host 0.0.0.0 --port $PORT voicevox_engine.app:app"]

