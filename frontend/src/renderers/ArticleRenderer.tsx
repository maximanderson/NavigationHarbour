//src\renderers\ArticleRenderer.tsx
import type { ArticleContentConfig } from "../types/portal";

interface ArticleRendererProps {
  content: ArticleContentConfig;
}

export function ArticleRenderer({ content }: ArticleRendererProps) {
  return (
    <article className="renderer article-renderer">
      <h2>{content.title}</h2>
      <p>{content.text}</p>
    </article>
  );
}