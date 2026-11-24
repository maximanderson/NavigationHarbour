# Portal Core — Dashboard Navigation Portal

Portal Core is a configuration-driven web application for organising and accessing dashboards, reports and other web-based resources through a common navigation interface.

It is intended primarily for corporate intranet environments where dashboards and operational information are distributed across multiple systems, such as Grafana, Power BI and PI Vision.

Portal Core provides configurable screens, hierarchical navigation trees, search and content presentation. Navigation structures are defined using JSON files, while screen configuration is maintained in `portal.ini`.

Existing dashboards remain hosted by their original applications and are displayed through browser iframes where embedding is permitted.

Portal Core does not replace the underlying applications or manage their authentication and authorisation.

## Purpose and Scope

Portal Core is intended to provide a common navigation and presentation layer for existing corporate resources.

Its primary responsibilities are:

- Organising dashboards and other resources into configurable navigation structures.
- Providing searchable navigation across large collections of dashboards.
- Presenting existing web applications without migrating or duplicating their underlying data.
- Providing access to locally hosted HTML content and other supported resources.

Portal Core is not a dashboard development platform, data management system or identity management solution.

It is designed primarily for use within trusted corporate network environments. Access to Portal Core itself must be controlled through the deployment environment where required.

## How it works

```text
 navigation JSON  ──►  navigation tree (left)  ──►  click a leaf  ──►  dashboard opens in an iframe (right)
 portal.ini       ──►  tabs, screens, theme
```

1. **`portal.ini`** defines the portal: its title, its screens (each screen is a tab) and where each screen's navigation file is.
2. **A navigation JSON file** defines the tree of items. An item with a `url` is a dashboard. An item with `children` is a branch.
3. Portal Core shows the tree, with search. Picking an item loads its `url` in the content area.
4. Optionally, a **Branch Screen** (a second small JSON file) offers buttons that jump straight to a branch.

## Windows quick start

A Windows x64 self-contained test package is available as the `v1.0.0` pre-release. The distribution includes the application and required .NET runtime; it does not require Node.js or IIS on the target computer.

Extract the package to a folder of your choice (for example, `D:\Applications\PortalCore`), open PowerShell in that directory and run:

```powershell
Copy-Item conf\server.sample.ini conf\server.ini
.\bin\PortalCore.Api.exe --home . --init-db
$env:ASPNETCORE_ENVIRONMENT = "Production"
.\bin\PortalCore.Api.exe --home .
```

Open `http://127.0.0.1:5247/` in a browser. The packaged demonstration uses fictional data. The application runs in the foreground until stopped; installing it as a service is a separate step and has not yet been validated for this pre-release.

For configuration, HTTPS and Windows service guidance, see [Windows deployment](docs/windows-deployment.md) and [release packaging](docs/release-package.md).

## Configure JSON navigation

Ready-made examples are in [`examples/`](examples/). All data is fictional.

### The navigation file

A navigation file is a JSON **array** of nodes. This is an excerpt from [`examples/navigation/example-navigation.json`](examples/navigation/example-navigation.json):

```json
[
  {
    "id": "riverton",
    "title": "Riverton Utilities (Example)",
    "children": [
      {
        "id": "water",
        "title": "Water Treatment",
        "children": [
          {
            "id": "water-plant",
            "title": "Riverton Treatment Plant",
            "children": [
              { "id": "water-plant-overview", "title": "Plant Overview",
                "url": "https://grafana.example.com/d/riverton-overview" },
              { "id": "water-plant-filtration", "title": "Filtration",
                "url": "https://grafana.example.com/d/riverton-filtration" }
            ]
          }
        ]
      },
      {
        "id": "energy",
        "title": "Energy",
        "children": [
          { "id": "energy-consumption", "title": "Energy Consumption",
            "url": "https://powerbi.example.com/reports/energy-consumption" }
        ]
      }
    ]
  }
]
```

| Field | Required | Meaning |
|---|---|---|
| `id` | **yes** | Unique within the file. A non-empty string (recommended) or a number. It is the identity of the node: the Branch Screen refers to it as `navID`, and the Usage Monitor records it. |
| `title` | **yes** | The text shown in the tree. The search looks at it. |
| `url` | no | The page to open in the iframe. A node with a `url` can open content, including when it also has `children`. |
| `children` | no | An array of more nodes, to any depth. |

Portal Core checks these rules when it loads the file. A bad file shows a clear error in the navigation panel, naming the position of the problem:

- the file must be an array, and every node must be an object;
- `id` must be present, non-empty and **unique across the whole tree**;
- `children`, if present, must be an array.

More detail is in [docs/navigation.md](docs/navigation.md).

### The configuration

[`examples/config/portal.ini`](examples/config/portal.ini) loads the example. The important part:

```ini
[portal]
title = Riverton Utilities
defaultScreen = dashboards
[screen.dashboards]
title = Dashboards
layout = left
[screen.dashboards.nav.left]
title = Riverton Navigation
type = tree
source = /navigation/example-navigation.json
search = true
searchMinChars = 3
searchAttributes = title
[screen.dashboards.content]
renderer = iframe
defaultUrl =
```

- Every `[screen.<id>]` becomes a **tab**. `defaultScreen` is the tab that opens first.
- `source` is a URL on the portal itself: `/navigation/<file>.json` is the file `navigation/<file>.json` in the public folder.
- `defaultUrl` is what the content area shows before anything is picked. Leave it empty for a short "select an item" message.
- **Comments go on their own line**, starting with `#` or `;`. Text after a value is part of the value, so `port = 5247 # main` would not work.

### Try it

**With the Windows package** (see [Installation](#installation-and-configuration)): copy the example files into the package's public folder and reload the page in your browser. Portal Core does not need a restart. If the browser keeps showing the old file, force a refresh (Ctrl+F5).

```text
examples/navigation/example-navigation.json  ->  PortalCore/public/navigation/
examples/navigation/example-branches.json    ->  PortalCore/public/navigation/
examples/config/portal.ini                   ->  PortalCore/public/config/portal.ini   (keep a copy of the old one)
```

**While developing**: copy the same files into `frontend/public/navigation/` and `frontend/public/config/`, then run `npm run dev` in `frontend/`. The example does not need the backend, because the Usage Monitor and the AI placeholder are switched off when `portal.ini` does not enable them.

### Use your own dashboards

1. Copy `example-navigation.json` and give it your own name.
2. Replace each `url` with the address of one of your dashboards, and change the `title`s.
3. Keep every `id` unique. Add or remove branches and leaves freely.
4. Point `source` in `portal.ini` at your file.
5. Reload the page.

Before a dashboard appears inside Portal Core, check [iframe embedding](#security-and-iframe-considerations): the dashboard's own system has to allow it.

### How it looks in Portal Core

The JSON above becomes this tree. A branch has a ▶ / ▼ toggle, and a node with a `url` is a clickable dashboard:

```text
▼ Riverton Utilities (Example)
  ▼ Water Treatment
    ▼ Riverton Treatment Plant
        Plant Overview         <- click: opens its url in the right-hand pane
        Filtration
  ▶ Energy
```

- **Search** (when `search = true`): type `searchMinChars` or more characters (default 3) and the tree filters to the matching titles, with their parent branches opened. `*` is a wildcard, so `flow*pump` matches "Flow and Pump". Clearing the box restores the tree. `searchAttributes` names which fields are searched; the default is `title`.
- **A node with both `children` and a `url`** works: the toggle opens the branch, and clicking the title opens the dashboard.
- **Tabs:** clicking the tab you are already on resets that screen.

### Optional: the Branch Screen

A second JSON file turns the opening page of a screen into a menu of buttons. Picking a button **re-roots the tree** on that branch, so the navigation shows only that part. [`examples/navigation/example-branches.json`](examples/navigation/example-branches.json):

```json
[
  { "title": "SITES",
    "branches": [
      { "title": "RIVERTON TREATMENT PLANT", "navID": "water-plant" },
      { "title": "LAKESIDE PUMP STATION",    "navID": "water-pumps" }
    ] },
  { "title": "Home",
    "branches": [ { "title": "Restore menu", "navID": "riverton" } ] }
]
```

- A file is an array of **columns**. Each column has a `title` and a `branches` array of buttons.
- Each button has a `title` and a `navID`. A `navID` must be the `id` of a node in the same navigation file (a string or a number). The `water-pumps` node is in the complete example file, not the shortened excerpt above.
- Enable it on a screen with two lines in its `[screen.<id>.content]` section: `defaultRenderer = branches` and `branchesSource = /navigation/example-branches.json`.
- Pointing a button at the top node (here `riverton`) gives a "Restore menu" button that brings the whole tree back. Picking any dashboard hides the Branch Screen and shows that dashboard.
- If the branches file cannot be loaded, Portal Core falls back to the normal content area.

## Key features

- **JSON-driven navigation tree** of any depth, with search and wildcards.
- **Branch Screen** for large trees: pick a branch, and the tree narrows to it.
- **Several screens** as tabs, each with its own navigation and content.
- **Iframe presentation** of Grafana, Power BI, PI Vision or any other web page, plus **HTML content** served by Portal Core itself.
- **Usage Monitor** (optional): records which screen and item was opened, with a timestamp, in a local SQLite database.
- **Themes:** `light`, `dark` and `industrial` (set with `theme` in `[portal]`).
- **Standalone hosting:** one Kestrel process serves the portal, the API and the public files. HTTP and HTTPS (PFX or PEM) are supported. IIS is not needed.
- **Windows x64 self-contained package:** no .NET, Node.js or IIS needed on the target PC.
- **Database backup and restore** from the command line, with a maintenance lock.
- **Configurable logging** (browser console, and backend console and file) — see [docs/logging.md](docs/logging.md).

## Installation and configuration

| I want to… | Read |
|---|---|
| Run the **Windows package** (download, extract, start, install as a service with NSSM) | [docs/release-package.md](docs/release-package.md) and [docs/windows-deployment.md](docs/windows-deployment.md) |
| Understand **`server.ini`** (address, port, HTTPS certificates, paths, logging) | [docs/server-configuration.md](docs/server-configuration.md) |
| Understand **standalone hosting** and what is web-accessible | [docs/standalone-hosting.md](docs/standalone-hosting.md) |
| Run behind a **reverse proxy** (IIS ARR, Nginx, Apache) and record the real client IP | [docs/reverse-proxy.md](docs/reverse-proxy.md) |
| **Back up and restore** the database | [docs/backup-restore.md](docs/backup-restore.md) |
| Configure **renderers and iframe options** | [docs/renderer-system.md](docs/renderer-system.md) |
| Write **navigation files** in detail | [docs/navigation.md](docs/navigation.md) |

Two configuration files, with different audiences:

| File | Purpose | Web-accessible? |
|---|---|---|
| `public/config/portal.ini` | The **portal**: screens, navigation, themes, iframe options | Yes, served to browsers |
| `conf/server.ini` | The **server**: port, HTTPS, folders, logging | **No.** Private. It is never served and may hold a certificate password |

The folders that browsers can reach are `public/config`, `public/navigation` and `public/content`. The server is designed to serve these public directories, not private server configuration or database files.

**Develop from source** (.NET 10 SDK, and Node.js 20.19+ or 22.12+):

```bash
cd frontend && npm ci && npm run dev         # the portal, with Vite, on http://localhost:5173
dotnet run --project backend/PortalCore.Api  # the API, on http://localhost:5247 (needed for the Usage Monitor)
dotnet test PortalCore.sln                   # backend tests
```

## Security and iframe considerations

**Portal Core presents; the source systems protect.** Keep these points in mind.

- **No login.** Portal Core has no authentication of its own. Anyone who can reach it can see the portal, its configuration and its navigation files. Do not put passwords, tokens or secret query strings in a `url`. If you need to restrict who can open it, use your organisation's network controls, or a reverse proxy that signs people in.
- **Dashboards stay protected by their own system.** Portal Core does not pass credentials to them or manage permissions. A user who cannot open a dashboard in Grafana, Power BI or PI Vision cannot open it in Portal Core either.
- **The Usage Monitor** stores the clicked item's id, title and URL, the screen, and the client IP address. Tell your users, and take that into account before enabling it.
- **Iframe permissions.** Portal Core does not add a `sandbox` attribute to its iframes. You can limit what a framed page may do with `allow` and `referrerPolicy`, for all screens or per screen:

  ```ini
  [renderer.iframe]
  # empty "allow": no extra browser permissions for framed pages
  allow =
  referrerPolicy = strict-origin-when-cross-origin
  ```

### Why a dashboard might not appear

An iframe shows a page only if that page agrees to be framed. If the content area stays blank or shows a browser error, check:

1. **Framing is forbidden by the dashboard's system.** Many products send an `X-Frame-Options` or a `Content-Security-Policy: frame-ancestors` header that blocks embedding. For Grafana, embedding is **off by default**. In `[security]` of its configuration, `allow_embedding = false`, and Grafana's own comment reads: "set to true if you want to allow browsers to render Grafana in a `<frame>`, `<iframe>`, `<embed>` or `<object>`. default is false." Grafana also ships with `[auth.anonymous] enabled = false`, and its login cookie defaults to `cookie_samesite = lax`. (Quoted from Grafana's `defaults.ini`; check your version.) Power BI and PI Vision have their own embedding rules and options. Check their documentation.
2. **Sign-in inside the frame.** A framed page that needs a login can fail because browsers may treat the frame as third-party and withhold its cookies, depending on `SameSite` and on whether the dashboard and Portal Core share a site. Hosting both under the same parent domain, or using the product's own embed or anonymous-view option, avoids most of this.
3. **HTTPS and HTTP mixed.** A portal served over HTTPS cannot show an `http://` page in a frame, because browsers block it. Use `https://` dashboard URLs when Portal Core uses HTTPS.
4. **Network reach.** The frame is loaded by the **user's browser**, not by the Portal Core server. The user's PC must be able to reach each dashboard's address.
5. **A wrong URL.** Open the `url` in a normal browser tab first. If it does not work there, it will not work in Portal Core.

## Technical architecture

| Layer | Technology |
|---|---|
| Frontend | React, TypeScript, Vite |
| Backend | ASP.NET Core on Kestrel (.NET 10), Entity Framework Core |
| Database | SQLite (the usage log) |
| Hosting | One self-contained process; standard ASP.NET Core static files for the portal and the public folders |
| Tests | xUnit (backend), Node test runner (frontend URL logic) |

Frontend component hierarchy:

```text
App → Portal → PortalScreen → NavigationPanel (tree + search)
                            → ContentRenderer → iframe | html | article | download | ai
```

- **Renderers:** `iframe` and `html` are configured in `portal.ini`. `ai` is a placeholder for a future assistant. `article` and `download` exist in the code but are not yet configurable from `portal.ini`.
- **Navigation:** `tree` is implemented. `list`, `tabs` and `buttons` are accepted in `portal.ini` but show a "not yet implemented" message. The navigation panel is shown at the left of a screen; `layout` is read from `portal.ini` but does not change the arrangement yet.
- **More detail:** [docs/component-hierarchy.md](docs/component-hierarchy.md), [docs/renderer-system.md](docs/renderer-system.md), [docs/file-structure.md](docs/file-structure.md).

## Future development — local AI

Portal Core contains an experimental `ai` renderer placeholder. **The current release does not include a functional AI assistant.**

A possible future use is a locally hosted Large Language Model (LLM) that helps employees locate information distributed across corporate intranets, SharePoint, IT self-service portals and other internal knowledge sources.

The intended scope is information explicitly approved for access by all members of the organisation. It would not include confidential customer records, sensitive financial information or material requiring individual document-level authorisation. An assistant could provide relevant answers together with references and links to the original sources, which would remain responsible for maintaining the information.

This is a potential direction rather than an implementation commitment. Any future integration would require controls for source approval, information classification, freshness, retrieval quality, access to the AI service and operational security. Portal Core's existing navigation functionality does not depend on AI.

## Status

- A Windows x64 test build (`v1.0.0`) has been published as a pre-release. It uses fictional demonstration data only, and has not yet been validated as a Windows service.
- Not yet implemented: right, top and bottom navigation panels, the `list`, `tabs` and `buttons` navigation types, and `article` and `download` configuration from `portal.ini`.
- Theme roadmap: CSS variables for the colours, custom theme files, and a compact tree theme.

## License

Portal Core is licensed under the MIT License. See [LICENSE](LICENSE) for details.
