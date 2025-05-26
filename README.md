# Virtual AI Doctor – MVP (Realtime Speech ↔ Speech)

## 🎯 Overview
OpenAI Realtime API を使用した **音声→GPT応答音声** のリアルタイム往復システム。
司令塔となる **MCP (Main Control Program)** を中心に、将来的な機能拡張（Graph AI連携、医療検索等）に備えた拡張可能なアーキテクチャ。

**特徴:**
- ✅ Whisper・外部TTS不要（Realtime API一本化）
- ✅ 300ms以下の低レイテンシ音声応答
- ✅ Function Calling対応（将来のGraph AI連携準備済み）
- ✅ Unity WebSocketクライアント対応

## 📁 Project Structure

```
virtual-ai-doctor/
├── README.md                    # このファイル
├── .env                        # 環境変数（要作成）
├── mcp/                        # FastAPI Hub (Main Control Program)
│   ├── app/
│   │   ├── main.py            # FastAPIアプリケーション起動
│   │   ├── core/
│   │   │   └── config.py      # 環境変数管理
│   │   ├── routers/
│   │   │   └── realtime.py    # WebSocket音声中継 (/ws/audio)
│   │   └── services/
│   │       ├── function.py    # Function Call ハンドラー
│   │       └── openai_ws.py   # Realtime API ヘルパー
│   ├── requirements.txt       # Python依存関係
│   └── .env.sample           # 環境変数テンプレート
├── Unity/                     # Unityクライアント
│   └── DoctorDemo.unity      # デモシーン
└── docs/                     # ドキュメント
    ├── setup.md              # セットアップ手順
    └── adr/                  # Architecture Decision Records
        └── 0001-use-realtime-api.md
```

## 🚀 Quick Start

### 1. 環境構築
```bash
# プロジェクトをクローン
git clone <repository-url>
cd virtual-ai-doctor

# Backend環境セットアップ
cd mcp
python -m venv .venv
source .venv/bin/activate  # Windows: .venv\Scripts\activate

# 依存関係インストール
pip install -r requirements.txt

# 環境変数設定
cp .env.sample .env
# .envファイルにOpenAI APIキーを設定してください
```

### 2. サーバー起動
```bash
# MCPサーバー起動
uvicorn app.main:app --reload

# 起動確認
curl http://127.0.0.1:8000/health
# 期待される応答: {"status": "healthy", "service": "realtime-audio-relay"}
```

### 3. Unityクライアント
```bash
# Unity Hub で Unity/DoctorDemo.unity を開く
# ▶ ボタンでシーン実行
```

## 🌐 API Endpoints

| Endpoint | Method | Purpose | Format |
|----------|--------|---------|---------|
| `/ws/audio` | WebSocket | リアルタイム音声通信 | PCM16 50msチャンク |
| `/health` | GET | サーバー状態確認 | JSON |
| `/docs` | GET | API仕様書（自動生成） | Swagger UI |

## 🔧 Configuration

### 必要な環境変数 (.env)
```env
OPENAI_API_KEY=sk-your-openai-api-key-here
OPENAI_REALTIME_ENDPOINT=wss://api.openai.com/v1/audio/livestream
```

### システム要件
- Python 3.10+
- Unity 2022.3 LTS+
- OpenAI API アクセス権限（Realtime API対応）

## 🛠 Future Extensions

| Phase | 機能 | 実装場所 |
|-------|------|----------|
| **Phase 1** | `search_medical` Node | `mcp/app/services/function.py` の `function_router` |
| **Phase 2** | Graph AI 連携 | 同 handler内で `subprocess.run(["graphai", "run", yaml])` |
| **Phase 3** | 音声認識ログ保存 | `realtime.py` の `openai_to_client()` 内 |
| **Phase 4** | 患者DB・RAG統合 | 新規 `services/database.py` |

## 📚 Documentation
- [セットアップ詳細手順](docs/setup.md)
- [アーキテクチャ決定記録](docs/adr/)
- [API仕様書](http://127.0.0.1:8000/docs) (サーバー起動後アクセス)

## 🐛 Troubleshooting
よくある問題と解決方法は [セットアップガイド](docs/setup.md#troubleshooting) を参照してください。

## 🤝 Contributing
1. 新機能は `mcp/app/services/` 配下に実装
2. Function Callは `function_router` に登録
3. API変更時は `/docs` で仕様確認

## 📄 License
[LICENSE](LICENSE) ファイルを参照
