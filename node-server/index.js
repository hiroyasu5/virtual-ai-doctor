const express = require("express");
const multer = require("multer");
const fs = require("fs");
const axios = require("axios");
const cors = require("cors");
const FormData = require("form-data");

const app = express();
const port = process.env.PORT || 3000;

app.use(cors());
app.use(express.json());

const upload = multer({ dest: "uploads/" });

// ✅ Whisperエンドポイント（音声文字起こし）
// server/index.js もしくは ルートの index.js
// ────────────────────────────────────

app.post("/transcribe", upload.single("audio"), async (req, res) => {
  const apiKey = req.body.user_api_key || process.env.OPENAI_API_KEY;
  const filePath = req.file.path;

  if (!apiKey) return res.status(400).json({ error: "APIキーがありません" });

  try {
    const formData = new FormData();
    // ← ここを修正
    formData.append(
      "file",
      fs.createReadStream(filePath),
      {
        filename: req.file.originalname || "recorded.wav",
        contentType: req.file.mimetype || "audio/wav"
      }
    );
    formData.append("model", "whisper-1");

    const response = await axios.post(
      "https://api.openai.com/v1/audio/transcriptions",
      formData,
      { headers: { 
          Authorization: `Bearer ${apiKey}`,
          ...formData.getHeaders()
      } }
    );

    fs.unlinkSync(filePath);
    res.json({ text: response.data.text });
  } catch (error) {
    console.error("Whisperエラー:", error.response?.data || error.message);
    res.status(500).json({ error: "Whisper文字起こし失敗" });
  }
});


// ✅ ChatGPTエンドポイント（チャット）
app.post("/chat", async (req, res) => {
  const { messages, user_api_key } = req.body;
  const apiKey = user_api_key || process.env.OPENAI_API_KEY;

  if (!apiKey) {
    return res.status(400).json({ error: "APIキーがありません" });
  }

  try {
    const response = await axios.post(
      "https://api.openai.com/v1/chat/completions",
      {
        model: "gpt-4o",
        messages,
      },
      {
        headers: {
          Authorization: `Bearer ${apiKey}`,
          "Content-Type": "application/json",
        },
      }
    );

    res.json(response.data);
  } catch (error) {
    console.error("ChatGPTエラー:", error.response?.data || error.message);
    res.status(500).json({ error: "ChatGPTリクエスト失敗" });
  }
});

app.listen(port, () => {
  console.log(`✅ サーバー起動中 → http://localhost:${port}`);
});

