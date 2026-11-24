# Navigation Harbour Renderer System

## Purpose

The renderer system is responsible for displaying different types of portal content using a unified, extensible architecture.

The system is config-driven and allows Navigation Harbour to support multiple content types while keeping screen logic simple.

---

## High-Level Architecture

```text
PortalScreen
    ↓
ContentRenderer
    ↓
Specific Renderer
```

`PortalScreen` does not need to know how each content type is displayed. It passes the screen content configuration to `ContentRenderer`, and `ContentRenderer` selects the correct renderer based on `content.renderer`.

---

## Runtime Flow

```text
1. PortalScreen receives screen configuration
2. PortalScreen passes content config to ContentRenderer
3. ContentRenderer checks content.renderer
4. ContentRenderer loads the matching renderer component
5. The specific renderer displays the content
```

For AI content, the runtime flow is:

```text
AiRenderer.tsx
    ↓
POST /api/ai/query
    ↓
Portal.Api
    ↓
Placeholder AI response
```

Current placeholder backend response:

```json
{
  "answer": "Hello! How are you? :)"
}
```

---

## Component Hierarchy

```text
PortalScreen.tsx
    ↓
ContentRenderer.tsx
    ├── IFrameRenderer.tsx
    ├── ArticleRenderer.tsx
    ├── DownloadRenderer.tsx
    ├── HtmlRenderer.tsx
    └── AiRenderer.tsx
```

---

## Renderer Types

Current supported renderer types are defined in `frontend/src/types/portal.ts`:

```ts
export type RendererType = "iframe" | "article" | "download" | "ai" | "html";
```

---

## iframe

Used for:

- PI Vision dashboards
- Grafana dashboards
- External systems
- SCADA displays

### Config Example

```ini
[screen.scada.content]
renderer = iframe
defaultUrl = https://example.com
```

### iframe Options

The `allow` (Permissions Policy delegation) and `referrerPolicy` attributes of the
iframe are configurable.

Global defaults:

```ini
[renderer.iframe]
allow = accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share
referrerPolicy = strict-origin-when-cross-origin
```

Optional per-screen overrides in `[screen.X.content]`:

```ini
[screen.regions.content]
renderer = iframe
allow = fullscreen; clipboard-write
referrerPolicy = no-referrer
```

Inheritance rules:

- Each property is resolved independently: screen value, else `[renderer.iframe]` value, else built-in default (the values shown above).
- A missing section or missing key falls back to the next level.
- An explicitly empty `allow =` overrides the inherited value and delegates no optional features.
- `allow` is passed to the browser as-is; it is not parsed or validated.
- `referrerPolicy` must be one of: `no-referrer`, `no-referrer-when-downgrade`, `origin`, `origin-when-cross-origin`, `same-origin`, `strict-origin`, `strict-origin-when-cross-origin`, `unsafe-url`.
- An empty or invalid `referrerPolicy` logs a console warning and falls back to the inherited valid value.

### Notes

- Supports dynamic URL switching from navigation nodes.
- Main renderer for operational dashboards.
- Uses an iframe as the rendering surface.

---

## article

Used for:

- Documentation
- Information pages
- Instructions
- Static text content

### Config Example

```ini
[screen.info.content]
renderer = article
title = Information
text = Welcome to Navigation Harbour
```

### Notes

- Suitable for static text screens.
- Does not require a navigation node URL.

---

## download

Used for:

- Downloadable documents
- Reports
- Reference files
- User guides

### Notes

- Intended for content where users need access to files rather than dashboards.
- Uses a dedicated download renderer component.

---

## html

Used for:

- Trusted static HTML content pages
- Internal report indexes
- Custom formatted information screens

### Config Example

```ini
[screen.reports]
title = Reports
layout = none

[screen.reports.content]
renderer = html
source = /content/reports.html
```

### Notes

- Fetches the HTML file from the configured `source` path at runtime.
- Renders HTML content directly into the page using `dangerouslySetInnerHTML`.
- Intended for trusted internal static HTML only.
- No upload, backend, markdown, or PDF functionality.
- HTML file is served from `frontend/public/content/`.

---

## ai

Used for:

- AI assistant screens
- Future local AI / RAG integration
- Questions about PI, AF, SCADA, Grafana and operational documentation

Example future questions:

```text
How can I find Happy Valley Tank flow?
Is tag adel.wger-TNT01-flow representing inflow into plant?
Where is tag XXXXX01 used?
Which dashboard uses this PI Point?
Which AF Element contains this PI Point?
Which SCADA SVG page displays this pump?
```

### Config Example

```ini
[screen.ai]
title = AI Help
layout = none

[screen.ai.content]
renderer = ai
defaultUrl =

[ai]
enabled = true
provider = local
protocol = http
host = localhost
port = 5247
endpoint = /api/ai/query
model = placeholder
```

### Frontend Implementation

The AI renderer is implemented in:

```text
frontend/src/renderers/AiRenderer.tsx
```

It provides:

- Question input textarea
- `Ask AI` button
- HTTP POST call to the backend AI endpoint
- Answer display panel
- Basic loading state
- Basic error handling

The AI renderer builds the API URL from the `[ai]` section in `portal.ini`:

```text
{protocol}://{host}:{port}{endpoint}
```

With the current configuration, this becomes:

```text
http://localhost:5247/api/ai/query
```

### Backend Implementation

The current backend endpoint is implemented in:

```text
backend/Portal.Api/Program.cs
```

Endpoint:

```http
POST /api/ai/query
```

Current placeholder response:

```json
{
  "answer": "Hello! How are you? :)"
}
```

This confirms the end-to-end plumbing before adding real AI complexity.

---

## AI Renderer Design Principle

`AiRenderer.tsx` should only handle the user interface and call the Navigation Harbour backend.

It should not directly connect to:

- Ollama
- LM Studio
- Open WebUI
- Vector databases
- Internal document stores
- File indexes

Those integrations should live behind the backend API.

Recommended future architecture:

```text
AiRenderer.tsx
    ↓
POST /api/ai/query
    ↓
Portal.Api
    ↓
Local AI / RAG service
    ↓
Vector DB + indexed internal documents
```

This keeps the frontend simple and keeps enterprise configuration, security and data access on the backend side.

---

## Future RAG Sources

Potential internal knowledge sources for an organisation-specific implementation:

```text
/docs/scada-tag-naming-standard.pdf
/navigation/*.json
/dashboards/grafana/*.json
/svg/scada/*.svg
/exports/pi-points.csv
/exports/af-attributes.csv
/exports/af-analysis.csv
/exports/pi-point-to-af-path.csv
```

Future RAG configuration may be added as:

```ini
[ai.rag]
enabled = true
vectorDbProvider = chroma
vectorDbPath = ./data/vector-db
documentsPath = ./data/documents
topK = 5
similarityThreshold = 0.75
```

---

## Adding a New Renderer

To add another renderer:

1. Add the renderer name to `RendererType` in `frontend/src/types/portal.ts`.
2. Add a specific content config interface if needed.
3. Create a renderer component in `frontend/src/renderers`.
4. Add a new case to `ContentRenderer.tsx`.
5. Update `configService.ts` if the renderer needs config parsing from `portal.ini`.
6. Add any required CSS to `App.css` or a renderer-specific stylesheet.
7. Update this document.

---

## Current Renderer Summary

| Renderer | Component | Purpose |
|---|---|---|
| `iframe` | `IFrameRenderer.tsx` | Dashboards and external systems |
| `article` | `ArticleRenderer.tsx` | Static text and information pages |
| `download` | `DownloadRenderer.tsx` | Downloadable files and reports |
| `html` | `HtmlRenderer.tsx` | Trusted static HTML content pages |
| `ai` | `AiRenderer.tsx` | AI assistant / future RAG interface |

---

## Current AI Implementation Status

Implemented now:

- `renderer = ai` support in `portal.ini`
- `[ai]` configuration section
- `AiConfig` type
- AI config parsing in `configService.ts`
- `AiRenderer.tsx`
- `ContentRenderer.tsx` AI case
- `PortalScreen.tsx` and `Portal.tsx` pass AI config down to renderer
- `POST /api/ai/query` backend endpoint
- Placeholder backend response

Not implemented yet:

- Real local LLM integration
- RAG document indexing
- Vector database search
- Source/reference display
- Conversation history
- Authentication/authorization around AI queries
- Question/answer audit logging
