
---

## `docs/component-hierarchy.md`

```md
# PortalCore Component Hierarchy frontend

## High-Level Flow

main.tsx
  ↓
App.tsx
  ↓
Portal.tsx
  ↓
PortalScreen.tsx
  ├── NavigationPanel.tsx
  │     ↓
  │   TreeView.tsx
  │
  └── ContentRenderer.tsx
        ├── IFrameRenderer.tsx
        ├── ArticleRenderer.tsx
        ├── DownloadRenderer.tsx
        └── HtmlRenderer.tsx