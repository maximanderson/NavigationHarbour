# Standalone Hosting (one Kestrel process)

In standalone mode a single Navigation Harbour process serves everything: the React portal, the API and the public
portal files. No IIS and no Vite server. Development is unchanged (Vite + Kestrel separately).

## Installation layout

```text
NavigationHarbour/                  <- Navigation Harbour home (--home / PORTAL_HOME, or found next to bin/)
├── bin/                     <- application (content root): NavigationHarbour.dll, appsettings.json, ...
│   └── wwwroot/             <- compiled React frontend (index.html, assets/)
├── conf/server.ini          <- private configuration
├── public/                  <- served: only config/, navigation/ and content/
│   ├── config/portal.ini
│   ├── navigation/*.json
│   └── content/*.html
├── data/usage.db       <- private
├── logs/  backups/  certs/  ai/   <- private
```

Home and content root are separate (see server-configuration.md). Files are never looked up relative to the
working directory.

## Building

```
scripts/build-standalone.sh [output-directory]        (Linux / macOS)
scripts\build-standalone.ps1 [-Out <directory>]       (Windows PowerShell)
```

The default output is `artifacts/NavigationHarbour`. The script runs the existing `npm run build` (Vite), `dotnet publish`
into `<out>/bin`, and copies the Vite output into `<out>/bin/wwwroot`. `config/`, `navigation/` and `content/` are
removed from `wwwroot` on purpose: they are served from `<out>/public`, so you can edit them without a rebuild.
On a new installation `<out>/public` is filled once from `frontend/public`; an existing `public` is never touched.
No executable packaging or service installation is part of this stage.

First start of a new installation: copy `conf/server.sample.ini` to `<home>/conf/server.ini`, then run
`NavigationHarbour --home <home> --init-db`, then start Navigation Harbour.

## What is served

| URL | Source |
|---|---|
| `/`, `/assets/*`, `/favicon.svg`, ... | `bin/wwwroot` (only when it contains `index.html`) |
| `/config/*` (`portal.ini` as `text/plain`) | `<[paths] public>/config` |
| `/navigation/*` | `<[paths] public>/navigation` |
| `/content/*` | `<[paths] public>/content` |
| `/api/*` | the backend. An unknown `/api/...` path gives a JSON `404`, never HTML |
| any other path without a file extension (client-side route) | `index.html` (SPA fallback) |

A missing file with an extension (for example `/assets/x.js`) is a plain `404`. Directory browsing is off, dotfiles are
not served, and the standard static file middleware and physical file providers are used throughout.

## Security boundaries

- Only the three folders above are web-accessible. The rest of `[paths] public` is not, so a stray file there is not exposed.
- `conf/`, `data/`, `logs/`, `backups/`, `certs/`, `ai/` and `bin/` have no URL.
- Startup refuses a `[paths] public` that contains or equals the home, `conf`, `data`, `logs`, `ai`, `backups` or the
  application directory (`bin/`). Certificate files inside `public` are refused too (see server-configuration.md).
- Path traversal (`..`, `%2e%2e`, encoded separators) cannot leave the three folders.
- Keep certificates in `certs/` or `conf/`, never in `public`. Do not put secrets into the three public folders.

## API addresses in `portal.ini`

`[usage_monitor]` and `[ai]` accept `protocol`, `host`, `port` and `endpoint`.

- **Standalone: leave `host` and `port` out (or `host` empty).** The portal calls `/api/usage` and `/api/ai/query`
  on the server it was loaded from, over the same protocol, so it works from any browser and behind HTTPS.
- **Development / external API:** with `host` set, the absolute URL `protocol://host:port/endpoint` is used, as
  before. A host without a port uses 5247; `port = 0` means the protocol default port.

The development `portal.ini` still names `localhost:5247`. Remove those two lines for the installed copy.

## Development workflow (unchanged)

Run Kestrel (`dotnet run`) and Vite (`npm run dev`) separately. Neither `bin/wwwroot` nor `public/` exists next to the
backend project, so no hosting is added and the existing ports and Vite behaviour stay as they are.

## Limits

No HTTP compression or cache-busting policy beyond the ASP.NET Core defaults, no sub-path hosting
(`root_url` must not contain a path), and no live reload of `wwwroot` (restart after replacing it).

Running behind a reverse proxy at the root of a host is possible; see [reverse-proxy.md](reverse-proxy.md) for
recording the real client address.
