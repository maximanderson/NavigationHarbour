# PortalCore

PortalCore is a config-driven enterprise portal designed to integrate operational and industrial systems into a single lightweight web interface.

## Features

- Config-driven screens
- Flexible navigation panels
- Multiple content renderers
- PI Vision integration
- Grafana integration
- Usage logging
- Configurable system logging (frontend console, backend console/file) — see [docs/logging.md](docs/logging.md)
- Extensible architecture

## Technology Stack

### Frontend
- React
- TypeScript
- Vite

### Backend
- ASP.NET Web API
- SQLite

## Current Renderer Types

- iframe
- article
- download
- ai (planned)

## Project Structure

Frontend hierarchy:

App
→ Portal
→ PortalScreen
→ ContentRenderer
→ Specific Renderers

## Goals

PortalCore aims to provide:

- Lightweight enterprise portal architecture
- Easy dashboard integration
- Config-driven extensibility
- Simple operational workflow
- Maintainable frontend structure

## Status

Current development stage:
- Architecture established
- Renderer system implemented
- Navigation framework implemented
- CSS/layout foundation in progress

## Theme Roadmap

- Phase 1: Apply theme class from `portal.ini`
- Phase 2: Convert current colours to CSS variables
- Phase 3: Add light/dark/industrial predefined themes
- Phase 4: Add custom theme file loading
- Phase 5: Add compact TreeView theme

## To Do List

- Complete CSS/layout foundation
- Support right, top, and bottom navigation panels
- Improve renderer-specific config parsing for article, download, and ai renderers
- Move more service settings into `portal.ini`

## License

Internal/private project.