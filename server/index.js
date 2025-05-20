// ✨ 新しい index.js にこのまま貼って使ってください
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
    console.error("❌ OpenAIエラー:", error.response?.data || error.message);
    res.status(500).json({ error: "OpenAI API リクエスト失敗" });
  }
});

