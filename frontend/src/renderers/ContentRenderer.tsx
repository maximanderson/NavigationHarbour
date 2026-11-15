import type { ContentConfig, AiConfig } from "../types/portal";
import { IFrameRenderer } from "./IFrameRenderer";
import { ArticleRenderer } from "./ArticleRenderer";
import { DownloadRenderer } from "./DownloadRenderer";
import { AiRenderer } from "./AiRenderer";
import { HtmlRenderer } from "./HtmlRenderer";

interface ContentRendererProps {
  content: ContentConfig;
  urlOverride?: string;
  reloadKey?: number;
  aiConfig?: AiConfig;
}

export function ContentRenderer({
  content,
  urlOverride,
  reloadKey,
  aiConfig,
}: ContentRendererProps) {
  switch (content.renderer) {
    case "iframe":
      return (
        <IFrameRenderer
          content={content}
          urlOverride={urlOverride}
          reloadKey={reloadKey}
        />
      );

    case "article":
      return <ArticleRenderer content={content} />;

    case "download":
      return <DownloadRenderer content={content} />;

    case "html":
      return <HtmlRenderer content={content} />;

    case "ai":
      return aiConfig ? (
        <AiRenderer content={content} config={aiConfig} />
      ) : (
        <div className="renderer ai-renderer">
          AI configuration missing.
        </div>
      );

    default: {
      const _exhaustiveCheck: never = content;
      return _exhaustiveCheck;
    }
  }
}
