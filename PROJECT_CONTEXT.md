# PortalCore

## Purpose

PortalCore is a config-driven enterprise portal designed to host and integrate:

- PI Vision dashboards
- Grafana dashboards
- Reports
- Documentation
- Future AI assistant capabilities

The portal acts as a lightweight shell/container for industrial and operational systems.

---

## Technology Stack

### Frontend
- React
- TypeScript
- Vite

### Backend
- ASP.NET Web API
- SQLite

### Source Control
- Git + GitHub

---

## Current Architecture

Frontend component hierarchy:

App
→ Portal
→ PortalScreen
→ ContentRenderer
→ Specific Renderers

---

## Portal Layout Concept

Portal supports:

- Top header/tabs
- Left/right/top/bottom navigation panels
- Main content area
- Config-driven screens

Main current layout:

Header
+ Left navigation
+ Main content (iframe/article/download/etc.)

---

## Content Renderers

Currently supported renderer types:

### iframe
Used for:
- PI Vision
- Grafana
- External dashboards

### article
Used for:
- Documentation
- Information pages

### download
Used for:
- File downloads
- Reports
- Templates

### ai (planned)
Future AI assistant integration.

---

## Navigation

Navigation is config-driven.

Supported navigation types:
- tree
- list
- tabs
- buttons

Current implementation focus:
- Tree navigation

---

## Current Focus

Current development priorities:

1. CSS layout and styling
2. Navigation usability
3. Responsive portal structure
4. Renderer consistency
5. Usage logging

---

## Design Philosophy

PortalCore is intended to be:

- Lightweight
- Config-driven
- Easy to maintain
- Enterprise-friendly
- Fast and predictable

The UI philosophy is closer to:
- Grafana
- PI Vision
- SCADA systems

rather than marketing-style web applications.

---

## Important Notes

- Portal must support large navigation structures (~2000 dashboards).
- Navigation performance and usability are critical.
- Main content area should fully support iframe-based systems.
- Layout must remain extensible for future AI integration.

---

## Future Ideas

Potential future features:

- Search/filter navigation
- Favorites/recent dashboards
- AI assistant panel
- Authentication integration
- User preferences
- Usage analytics
- Dynamic layout configuration