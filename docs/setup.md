# セットアップ & 起動手順

## 🎯 概要
このガイドに従えば、5分でVirtual AI Doctorが起動します。

## 📋 前提条件
- Python 3.10以上
- Git
- OpenAI APIキー（Realtime API アクセス権限付き）

## 🚀 インストール手順

### 1. プロジェクトのクローン
```bash
git clone <repository-url>
cd virtual-ai-doctor
```

### 2. Python環境のセットアップ
```bash
cd mcp
python -m venv .venv
source .venv/bin/activate    # Windows: .venv\Scripts\activate
pip install -r requirements.txt
```

### 3. 環境変数の設定
```bash
# テンプレートをコピー
cp .env.sample .env

# .envファイルを編集
OPENAI_API_KEY=sk-your-actual-api-key-here
OPENAI_REALTIME_ENDPOINT=wss://api.openai.com/v1/audio/livestream
```

### 4. サーバー起動
```bash
uvicorn app.main:app --reload
```

### 5. 動作確認
```bash
# 別ターミナルで実行
curl http://127.0.0.1:8000/health

# 期待される応答
{"status": "healthy", "service": "realtime-audio-relay"}
```

## 🔧 Troubleshooting

### `ModuleNotFoundError: No module named 'app'`
**原因**: 相対インポートの問題
**解決**: プロジェクトルートから `uvicorn mcp.app.main:app --reload` で起動

### `ModuleNotFoundError: No module named 'websockets'`
**原因**: 依存関係未インストール
**解決**: `pip install -r requirements.txt` を再実行

### `RuntimeError: OPENAI_* の環境変数が不足しています`
**原因**: `.env`ファイルの設定不備
**解決**: 
1. `.env`ファイルが存在するか確認
2. OpenAI APIキーが正しく設定されているか確認
3. APIキーが`sk-`で始まっているか確認

### サーバーは起動するがWebSocket接続できない
**原因**: ファイアウォールまたはポート問題
**解決**: 
1. `http://127.0.0.1:8000/docs` でSwagger UIアクセス確認
2. 他のアプリがポート8000を使用していないか確認

## 💡 開発モード
```bash
# ホットリロード有効で起動
uvicorn app.main:app --reload --host 0.0.0.0 --port 8000

# ログレベル変更
uvicorn app.main:app --reload --log-level debug
```

## 🧪 テスト
```bash
# ヘルスチェック
curl http://127.0.0.1:8000/health

# WebSocket接続テスト（wscat必要）
npm install -g wscat
wscat -c ws://127.0.0.1:8000/ws/audio
```
