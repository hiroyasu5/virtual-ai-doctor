# セットアップ手順

## 🚀 インストール
```bash
git clone https://github.com/hiroyasu5/virtual-ai-doctor.git
cd virtual-ai-doctor/mcp
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
cp .env.sample .env  # OpenAI APIキーを設定
uvicorn app.main:app --reload
```

ヘルスチェック:
```bash
curl http://127.0.0.1:8000/health
# {"status": "healthy", "service": "realtime-audio-relay"} が返ればOK
```

## 🎮 Unity設定
1. Unity Hub で `Unity/SampleScene.unity` を開く
2. Play ボタン → StatusText が "Connected" 表示を確認
3. VoiceChatSystem → AudioManager → Audio Source に AudioSource をドラッグ設定
4. MicButton クリック → 「Hello, how are you?」と話す → AI音声応答を確認

## 🎯 動作テスト
- **WebSocket接続**: StatusText が "Connected"
- **音声録音**: MicButton で録音開始/停止
- **AI応答**: OpenAIからの音声が再生される
- **アバター**: VRoidキャラクターがアニメーション

## 🔧 よくある問題

### FastAPI起動エラー
```bash
# モジュールエラーの場合
cd virtual-ai-doctor
uvicorn mcp.app.main:app --reload
```

### Unity接続エラー
- FastAPIサーバーが起動中か確認
- WebSocketManager の Server URL が `ws://127.0.0.1:8000/ws/audio` か確認

### 音声が出ない
- AudioSource 参照が設定されているか確認
- システム音量確認
- マイクアクセス許可確認

### 環境変数エラー
`.env` ファイルの設定:
```env
OPENAI_API_KEY=sk-your-actual-api-key-here
OPENAI_REALTIME_ENDPOINT=wss://api.openai.com/v1/audio/livestream
```

## 💡 開発メモ
- **Sample Rate**: 16kHz (OpenAI推奨)
- **音声フォーマット**: PCM16
- **レイテンシ目標**: 300ms以下

完成すれば音声でAIと会話できます！🎤✨
