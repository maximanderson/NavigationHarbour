import { useState } from "react";
import type { AiContentConfig, AiConfig } from "../types/portal";
import { buildApiUrl } from "../utils/urlBuilder";
import { logger } from "../services/logger";

interface AiRendererProps {
  content: AiContentConfig;
  config: AiConfig;
}

interface AiResponse {
  answer: string;
}

export function AiRenderer({ content, config }: AiRendererProps) {
  const [question, setQuestion] = useState("");
  const [answer, setAnswer] = useState("");
  const [loading, setLoading] = useState(false);

  async function handleAsk() {
    if (!question.trim()) {
      return;
    }

    setLoading(true);

    try {
      const response = await fetch(buildApiUrl(config), {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
           prompt: question,
        }),
      });

      const data = (await response.json()) as AiResponse;

      setAnswer(data.answer ?? "No response returned.");
    } catch (err) {
      logger.error("AI request failed:", err);
      setAnswer("Failed to contact AI backend.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="ai-renderer">
      <div className="ai-renderer-header">
        <h2>{content.title || "AI Assistant"}</h2>
        {content.text && <p>{content.text}</p>}
      </div>

      <div className="ai-renderer-body">
        <textarea
          className="ai-question-input"
          placeholder="Ask a question about PI, AF, SCADA, Grafana or dashboards..."
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
        />

        <button
          className="ai-ask-button"
          onClick={handleAsk}
          disabled={loading}
        >
          {loading ? "Thinking..." : "Ask AI"}
        </button>

        <div className="ai-answer-panel">
          {answer || "AI response will appear here."}
        </div>
      </div>
    </div>
  );
}
