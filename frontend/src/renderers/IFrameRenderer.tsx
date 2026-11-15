//src\renderers\IFrameRenderer.tsx
import type { IFrameContentConfig } from "../types/portal";

interface IFrameRendererProps {
  content: IFrameContentConfig;
  urlOverride?: string;
  /** Changing this value remounts the iframe, forcing a reload of the URL. */
  reloadKey?: number;
}

export function IFrameRenderer({
  content,
  urlOverride,
  reloadKey,
}: IFrameRendererProps) {
  const url = urlOverride ?? content.defaultUrl;

  if (!url) {
    return (
      <div className="renderer iframe-renderer-empty">
        Select an item from the navigation panel.
      </div>
    );
  }

  return (
   <iframe
    key={reloadKey}
    src={url}
    className="renderer iframe-renderer"
    title="Portal content"
    allow={content.allow}
    referrerPolicy={content.referrerPolicy}
  />
  );
}