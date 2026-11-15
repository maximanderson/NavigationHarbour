import type { DownloadContentConfig } from "../types/portal";

interface DownloadRendererProps {
  content: DownloadContentConfig;
}

export function DownloadRenderer({ content }: DownloadRendererProps) {
  return (
    <div className="renderer download-renderer">
      <h2>{content.title}</h2>

      {content.text && <p>{content.text}</p>}

      <ul>
        {content.files.map((file) => (
          <li key={file.url}>
            <a href={file.url} download={file.fileName}>
              {file.label}
            </a>
          </li>
        ))}
      </ul>
    </div>
  );
}