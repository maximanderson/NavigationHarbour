# Navigation Harbour

**Unified navigation across trusted web applications.**

Navigation Harbour is an open-source, configurable navigation portal that orchestrates the presentation of existing web applications through iframes within a trusted environment.

Previously known as Portal Core.

## Why Navigation Harbour exists

It started with a simple requirement: a configurable navigation menu bringing together dashboards from different web applications through iframes within a trusted corporate environment.

I was almost certain that an open-source, ready-to-use solution already existed. To my surprise, I could not find one that met this particular need without introducing unnecessary complexity.

Perhaps the reason is that this is a relatively narrow use case.

Rather than building a solution tied to a particular organisation or technology, I decided to create a generic application that does one specific job and does it well.

Navigation Harbour is not intended to become another enterprise platform. It is simply a practical tool for bringing existing web applications together under a common navigation interface.

I am making it available as open-source software in the hope that someone facing a similar problem may find it useful.

**Not every problem needs a big solution.**

## What it does

- Shows a configurable navigation tree, with search, for dashboards, reports and other web pages.
- Opens the selected item in an iframe next to the tree. The application behind it stays where it is.
- Offers several screens as tabs, each with its own navigation and content.
- Serves locally hosted HTML content alongside the embedded applications.
- Optionally records which items were opened (Usage Monitor).

Navigation is defined in JSON files. Screens, themes and options are defined in `portal.ini`.

```text
 navigation JSON  ──►  navigation tree (left)  ──►  click a leaf  ──►  page opens in an iframe (right)
 portal.ini       ──►  tabs, screens, theme
```

1. **`portal.ini`** defines the portal: its title, its screens (each screen is a tab) and where each screen's navigation file is.
2. **A navigation JSON file** defines the tree of items. An item with a `url` opens a page. An item with `children` is a branch.
3. Navigation Harbour shows the tree, with search. Picking an item loads its `url` in the content area.
4. Optionally, a **Branch Screen** (a second small JSON file) offers buttons that jump straight to a branch.

## What it does not do

- It does not replace the applications it presents, and it does not copy or migrate their data.
- It has **no login of its own**. It does not authenticate users or manage permissions. See [Security and deployment considerations](#security-and-deployment-considerations).
- It does not pass credentials to embedded applications.
- It is not a dashboard builder, a data platform or an identity management solution.
- It does not include a functional AI assistant. An `ai` renderer exists only as a placeholder.

## Key features

- **JSON-driven navigation tree** of any depth, with search and wildcards.
- **Branch Screen** for large trees: pick a branch, and the tree narrows to it.
- **Several screens** as tabs, each with its own navigation and content.
- **Iframe presentation** of Grafana, Power BI, PI Vision or any other web page that permits embedding, plus **HTML content** served by Navigation Harbour itself.
- **Usage Monitor** (optional): records which screen and item was opened, with a timestamp, in a local SQLite database.
- **Themes:** `light`, `dark` and `industrial` (set with `theme` in `[portal]`).
- **Standalone hosting:** one Kestrel process serves the portal, the API and the public files. HTTP and HTTPS (PFX or PEM) are supported. IIS is not needed.
- **Windows x64 self-contained package:** no .NET, Node.js or IIS needed on the target PC.
- **Database backup and restore** from the command line, with a maintenance lock.
- **Configurable logging** (browser console, and backend console and file) — see [docs/logging.md](docs/logging.md).

## Architecture

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

- **Renderers:** `iframe` and `html` are configured in `portal.ini`. `ai` is a placeholder only. `article` and `download` exist in the code but are not yet configurable from `portal.ini`.
- **Navigation:** `tree` is implemented. `list`, `tabs` and `buttons` are accepted in `portal.ini` but show a "not yet implemented" message. The navigation panel is shown at the left of a screen; `layout` is read from `portal.ini` but does not change the arrangement yet.
- **More detail:** [docs/component-hierarchy.md](docs/component-hierarchy.md), [docs/renderer-system.md](docs/renderer-system.md), [docs/file-structure.md](docs/file-structure.md).

## Security and deployment considerations

**Navigation Harbour presents; the source systems protect.**

- **Embedded applications keep their own authentication and authorisation.** Navigation Harbour does not sign users in to them, pass credentials to them or manage their permissions. A user who cannot open a page in Grafana, Power BI or PI Vision directly cannot open it in Navigation Harbour either.
- **No login for Navigation Harbour itself.** Anyone who can reach it can see the portal, its configuration and its navigation files. Do not put passwords, tokens or secret query strings in a `url`. To restrict who can open it, use network controls or a reverse proxy that signs people in.
- **Designed for trusted environments**, such as a controlled internal network.
- **The Usage Monitor** stores the clicked item's id, title and URL, the screen, and the client IP address. Tell your users, and take that into account before enabling it.
- **Iframe permissions.** Navigation Harbour does not add a `sandbox` attribute to its iframes. You can limit what a framed page may do with `allow` and `referrerPolicy`, for all screens or per screen:

  ```ini
  [renderer.iframe]
  # empty "allow": no extra browser permissions for framed pages
  allow =
  referrerPolicy = strict-origin-when-cross-origin
  ```

- **Reverse proxy.** See [docs/reverse-proxy.md](docs/reverse-proxy.md) to run behind IIS ARR, Nginx or Apache and record the real client IP.

### Why a dashboard might not appear

An iframe shows a page only if that page agrees to be framed. If the content area stays blank or shows a browser error, check:

1. **Framing is forbidden by the application.** Many products send an `X-Frame-Options` or a `Content-Security-Policy: frame-ancestors` header that blocks embedding. For Grafana, embedding is **off by default** (`allow_embedding = false` in `[security]`). Grafana also ships with `[auth.anonymous] enabled = false`, and its login cookie defaults to `cookie_samesite = lax`. (From Grafana's `defaults.ini`; check your version.) Power BI and PI Vision have their own embedding rules. Check their documentation.
2. **Sign-in inside the frame.** A framed page that needs a login can fail because browsers may treat the frame as third-party and withhold its cookies, depending on `SameSite` and on whether the application and Navigation Harbour share a site. Hosting both under the same parent domain, or using the product's own embed or anonymous-view option, avoids most of this.
3. **HTTPS and HTTP mixed.** A portal served over HTTPS cannot show an `http://` page in a frame, because browsers block it. Use `https://` URLs when Navigation Harbour uses HTTPS.
4. **Network reach.** The frame is loaded by the **user's browser**, not by the Navigation Harbour server. The user's PC must be able to reach each address.
5. **A wrong URL.** Open the `url` in a normal browser tab first. If it does not work there, it will not work in Navigation Harbour.

## Getting started

### Windows package

A Windows x64 self-contained package is available from [GitHub Releases](https://github.com/maximanderson/NavigationHarbour/releases). It includes the application and the required .NET runtime; it does not need Node.js or IIS on the target computer. See [Downloads and release status](#downloads-and-release-status) for which release to choose.

Extract the package to a folder of your choice (for example, `D:\Applications\NavigationHarbour`), open PowerShell in that directory and run:

```powershell
Copy-Item conf\server.sample.ini conf\server.ini
.\bin\NavigationHarbour.exe --home . --init-db
$env:ASPNETCORE_ENVIRONMENT = "Production"
.\bin\NavigationHarbour.exe --home .
```

Open `http://127.0.0.1:5247/` in a browser. The packaged demonstration uses fictional data. The application runs in the foreground until stopped; installing it as a service is a separate step and has not yet been validated for these releases.

### Develop from source

Requires the .NET 10 SDK, and Node.js 20.19+ or 22.12+:

```bash
cd frontend && npm ci && npm run dev         # the portal, with Vite, on http://localhost:5173
dotnet run --project backend/Portal.Api  # the API, on http://localhost:5247 (needed for the Usage Monitor)
dotnet test Portal.sln                   # backend tests
```

## Configuration and documentation

Two configuration files, with different audiences:

| File | Purpose | Web-accessible? |
|---|---|---|
| `public/config/portal.ini` | The **portal**: screens, navigation, themes, iframe options | Yes, served to browsers |
| `conf/server.ini` | The **server**: port, HTTPS, folders, logging | **No.** Private. It is never served and may hold a certificate password |

The folders that browsers can reach are `public/config`, `public/navigation` and `public/content`. The server is designed to serve these public directories, not private server configuration or database files.

| I want to… | Read |
|---|---|
| Run the **Windows package** (download, extract, start, install as a service with NSSM) | [docs/release-package.md](docs/release-package.md) and [docs/windows-deployment.md](docs/windows-deployment.md) |
| Understand **`server.ini`** (address, port, HTTPS certificates, paths, logging) | [docs/server-configuration.md](docs/server-configuration.md) |
| Understand **standalone hosting** and what is web-accessible | [docs/standalone-hosting.md](docs/standalone-hosting.md) |
| Run behind a **reverse proxy** (IIS ARR, Nginx, Apache), or **share an existing HTTPS website** with other web applications | [docs/reverse-proxy.md](docs/reverse-proxy.md) |
| **Back up and restore** the database | [docs/backup-restore.md](docs/backup-restore.md) |
| Configure **renderers and iframe options** | [docs/renderer-system.md](docs/renderer-system.md) |
| Write **navigation files** in detail | [docs/navigation.md](docs/navigation.md) |
| See ready-made, fictional **examples** | [examples/](examples/) |

### Configure JSON navigation

Ready-made examples are in [`examples/`](examples/). All data is fictional.

#### The navigation file

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

Navigation Harbour checks these rules when it loads the file. A bad file shows a clear error in the navigation panel, naming the position of the problem:

- the file must be an array, and every node must be an object;
- `id` must be present, non-empty and **unique across the whole tree**;
- `children`, if present, must be an array.

More detail is in [docs/navigation.md](docs/navigation.md).

#### The configuration

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

#### Try it

**With the Windows package** (see [Getting started](#getting-started)): copy the example files into the package's public folder and reload the page in your browser. Navigation Harbour does not need a restart. If the browser keeps showing the old file, force a refresh (Ctrl+F5).

```text
examples/navigation/example-navigation.json  ->  NavigationHarbour/public/navigation/
examples/navigation/example-branches.json    ->  NavigationHarbour/public/navigation/
examples/config/portal.ini                   ->  NavigationHarbour/public/config/portal.ini   (keep a copy of the old one)
```

**While developing**: copy the same files into `frontend/public/navigation/` and `frontend/public/config/`, then run `npm run dev` in `frontend/`. The example does not need the backend, because the Usage Monitor and the AI placeholder are switched off when `portal.ini` does not enable them.

#### Use your own dashboards

1. Copy `example-navigation.json` and give it your own name.
2. Replace each `url` with the address of one of your dashboards, and change the `title`s.
3. Keep every `id` unique. Add or remove branches and leaves freely.
4. Point `source` in `portal.ini` at your file.
5. Reload the page.

Before a dashboard appears inside Navigation Harbour, check [iframe embedding](#why-a-dashboard-might-not-appear): the dashboard's own system has to allow it.

#### How it looks in Navigation Harbour

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

#### Optional: the Branch Screen

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
- If the branches file cannot be loaded, Navigation Harbour falls back to the normal content area.

## Downloads and release status

Downloads are on the [GitHub Releases](https://github.com/maximanderson/NavigationHarbour/releases) page.

| Release | Product name | Status |
|---|---|---|
| **v1.1.0** | Navigation Harbour | **Stable.** Adds the Navigation Harbour product identity, configurable at build time, and stable functional names. Manually accepted on Windows. Backward compatible with Portal Core installations (see [docs/windows-deployment.md](docs/windows-deployment.md)). |
| v1.1.0-rc.1 | Navigation Harbour | Release candidate for v1.1.0. Superseded. |
| v1.0.1 | Portal Core | Previous stable release. Adds client IP address resolution behind trusted reverse proxies; manually validated on Windows with a same-host IIS ARR proxy. Uses the Portal Core names. |

- The demonstration data is fictional. Running as a Windows service (NSSM) has not yet been validated for these releases.
- Portal Core is the former name of this project. The historical releases keep their original names.
- Not yet implemented: right, top and bottom navigation panels, the `list`, `tabs` and `buttons` navigation types, and `article` and `download` configuration from `portal.ini`.
- A possible future direction is a locally hosted AI assistant that helps people find information. The current releases do not include one, and this is not a commitment.
- Theme roadmap: CSS variables for the colours, custom theme files, and a compact tree theme.

## Licence

Navigation Harbour is licensed under the MIT Licence. Copyright (c) 2026 Maxim Andreev. See [LICENSE](LICENSE) for details. Third-party notices are in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
