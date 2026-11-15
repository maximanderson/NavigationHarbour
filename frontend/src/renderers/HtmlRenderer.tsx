//src\renderers\HtmlRenderer.tsx
import { useEffect, useState } from "react";
import type { HtmlContentConfig } from "../types/portal";

interface HtmlRendererProps {
  content: HtmlContentConfig;
}

export function HtmlRenderer({ content }: HtmlRendererProps) {
  const [html, setHtml] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetch(content.source)
      .then((res) => {
        if (!res.ok) {
          throw new Error(`Failed to load HTML content: ${content.source}`);
        }
        return res.text();
      })
      .then(setHtml)
      .catch((err: unknown) => {
        setError(err instanceof Error ? err.message : String(err));
      });
  }, [content.source]);

  if (error) {
    return <div className="renderer html-renderer-error">{error}</div>;
  }

  if (html === null) {
    return <div className="renderer html-renderer-loading">Loading content…</div>;
  }

  return (
    <div
      className="renderer html-renderer"
      // eslint-disable-next-line react/no-danger
      dangerouslySetInnerHTML={{ __html: html }}
    />
  );
}
