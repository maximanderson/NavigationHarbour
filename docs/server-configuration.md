# Server Configuration (`server.ini`)

Portal Core has two separate configuration files with different audiences:

| File | Purpose | Visible to browsers? |
|---|---|---|
| `public/config/portal.ini` (dev: `frontend/public/config/portal.ini`) | The **web portal**: screens, navigation, renderers, search, frontend behaviour | Yes, served as `/config/portal.ini` |
| `conf/server.ini` | The **server**: hosting, paths, logging, later secrets | **Never** |

> **Status: Stage 3A.** `server.ini` is loaded and validated, and can be inspected with
> `--check-config` / `--print-config`. `[log]`, `[paths] logs`, `[paths] ai` and `[paths] data`
> (SQLite location) are **applied** to the running application. `[server]` and `[paths] public`
> are validated only.
> See [Status of each setting](#status-of-each-setting).

## Location

`<home>/conf/server.ini`, with a documented template at `conf/server.sample.ini`.
Copy the sample to `server.ini` and edit it. `server.ini` is excluded from Git.
Upgrades must never overwrite it.

During development, `<home>` is the project directory, so the file is
`backend/PortalCore.Api/conf/server.ini`.

### How the home directory is found

In order; the working directory is never used:

1. `--home <directory>` argument.
2. `PORTALCORE_HOME` environment variable.
3. Installation layout: the executable is in `<home>/bin/` and `<home>` contains `conf`, `public` or `data`.
4. Development: the project directory (found by walking up from the build output to `PortalCore.Api.csproj`).

An explicit `--home` or `PORTALCORE_HOME` that does not exist is an error.

## Format

```ini
[server]
protocol = http
http_addr = 127.0.0.1
domain = localhost
port = 5247
; root_url = http://localhost:5247/

[paths]
data = data
logs = logs
public = public
ai = ai

[log]
level = info
console = true
file = true
max_file_size_mb = 10
retain_days = 14
```

- Comments are whole lines starting with `;` or `#`. **Text after a value is part of the value**
  (`port = 5247 ; main` is an error).
- Relative paths are relative to the home directory.
- Unknown sections and keys are errors (a "did you mean" hint is given).
- Keys are case-insensitive.

## Settings

| Setting | Default | Allowed values / notes |
|---|---|---|
| `[server] protocol` | `http` | `http`, `https` |
| `[server] http_addr` | `127.0.0.1` | IPv4/IPv6 address, `localhost`, or empty (all interfaces) |
| `[server] domain` | `localhost` | Bare host name, no scheme/port/path |
| `[server] port` | `5247` | 1–65535 |
| `[server] root_url` | *(unset)* | Absolute `http(s)://host[:port]/`; no credentials, path, query or fragment |
| `[tls] cert_file` | *(unset)* | Required for `https`. A PFX file, or the PEM certificate when `cert_key_file` is also set. Relative to home or absolute. |
| `[tls] cert_key_file` | *(unset)* | PEM private key (optional; PEM only). A path, not a secret. |
| `[tls] cert_password` | *(unset)* | Opens the PFX, or an encrypted PEM key. **Secret**: never printed or logged. |
| `[paths] data` / `logs` / `public` | `data` / `logs` / `public` | Directory, relative to home or absolute |
| `[paths] ai` | *(not set)* | Directory, relative to home or absolute. Knowledge is read from `<ai>/knowledge`, prompts from `<ai>/prompts`. When not set, the existing `Data/knowledge` and `Data/prompts` folders are used. |
| `[log] level` | `info` | `debug`, `info`, `warning`, `error` |
| `[log] console` / `file` | `true` | `true`, `false` |
| `[log] max_file_size_mb` / `retain_days` | `10` / `14` | Whole number > 0 |

Cross-checks: `[paths] public` must not contain `conf`, `data`, `logs` or `ai` (private files
would be served to browsers). Warnings: `http` on a non-loopback address, both log destinations
disabled, `[tls]` settings with `protocol = http` (ignored), and competing URL settings (see "HTTP and HTTPS").
## HTTP and HTTPS (Kestrel, Stage 4)

**When it applies.** If `protocol`, `http_addr` or `port` is set (in `server.ini`, an environment variable
or the command line), Portal Core configures Kestrel itself and listens on **exactly one** endpoint:
`protocol://http_addr:port` (empty `http_addr` = all interfaces). If none of the three is set, nothing changes:
`dotnet run` keeps using `launchSettings.json`, and `ASPNETCORE_URLS` / `--urls` work as before.

**Precedence.** When `[server]` configures the endpoint it wins over `ASPNETCORE_URLS`, `--urls`,
`ASPNETCORE_HTTP_PORTS` and `ASPNETCORE_HTTPS_PORTS`. The competing setting is ignored, and a warning is shown
by `--check-config` and written to the application log (the competing value itself is not printed).
A `Kestrel:Endpoints` section would add more listeners, so it is an error together with an explicit `[server]` endpoint.
`domain` and `root_url` are not used for binding.

**HTTPS.** `protocol = https` needs a certificate in `[tls]`:

```ini
; PFX (PKCS#12)
[tls]
cert_file = conf/portalcore.pfx
cert_password = change-me

; PEM certificate + separate private key (cert_password only when the key is encrypted)
[tls]
cert_file = conf/portalcore.pem
cert_key_file = conf/portalcore.key
```

- Relative paths are resolved against the Portal Core home, never the working directory.
- A certificate path inside `[paths] public` is an error, so certificates are never served. Keep them in `conf`.
- The certificate is loaded at startup with `X509CertificateLoader` (PFX), `CreateFromPemFile` or
  `CreateFromEncryptedPemFile` (PEM). It is re-imported in memory as PKCS#12, the usual way to make PEM keys
  usable by Kestrel on Windows. Windows has not been tested here; verify it during deployment.
- **No fallback.** A missing, unreadable, expired, wrong-password or mismatched certificate stops startup
  (exit code 2, "No listener was started"). Portal Core never serves plain HTTP instead of HTTPS, and never opens a second port.
- `[tls]` settings with `protocol = http` are ignored with a warning.

**Password.** `cert_password` may be written in `server.ini` (a private file: restrict access to the `conf` folder,
see Security) or supplied from outside, which is preferred for production:

- environment variable: `PORTALCORE_tls__cert_password=...` (or `--tls:cert_password=...`; user secrets in Development).
- These rank above `server.ini` (see Precedence). No other interpolation syntax exists: `${VAR}` is not expanded.

The password is never printed by `--print-config`, never written to the log and never put in an error message.
Messages say only that the password is missing or wrong.

**Diagnostics.** `--check-config` / `--print-config` validate the protocol, address, port, that `cert_file` is
set for https, that the files exist, are files and readable, and are not under `public`. They do **not** load the
certificate, start Kestrel or touch SQLite, so a file that is not a valid certificate (or a wrong password)
is found only when Portal Core starts. `--print-config` shows the effective endpoint.

**Limits.** No certificate renewal or reload (restart after replacing it), no client certificates, no HTTP-to-HTTPS
redirect, no certificate store lookup. Self-signed certificates work but browsers will warn.



## Precedence

Lowest to highest:

1. `appsettings.json` (shipped defaults)
2. `appsettings.Development.json` (Development only)
3. `conf/server.ini`
4. User secrets (Development only)
5. Environment variables, including `PORTALCORE_` variables
6. Command line

`PORTALCORE_` variables use `__` for sections: `PORTALCORE_server__port=5300`.
Command line: `--server:port=5300`.

Defaults for the `server.ini` settings are built into the application, so `appsettings.json` is unchanged.

### How `server.ini` reaches logging and AI

Only settings **written in `server.ini`** are applied. They are translated into the existing keys,
so there is no second logging implementation and no second source of truth:

| `server.ini` | Existing key used by the application |
|---|---|
| `[log] level` | `PortalLogging:Level` |
| `[log] console` | `PortalLogging:Console` |
| `[log] file` | `PortalLogging:File:Enabled` |
| `[log] max_file_size_mb` | `PortalLogging:File:MaxFileSizeMb` |
| `[log] retain_days` | `PortalLogging:File:RetainDays` |
| `[paths] logs` | `PortalLogging:File:Directory` (made absolute against home) |
| `[paths] ai` | `AiSettings:KnowledgeFolder` = `<ai>/knowledge`, `AiSettings:PromptFolder` = `<ai>/prompts` |

A setting left out of `server.ini` keeps its `appsettings.json` value. Because environment variables,
user secrets and the command line rank above `server.ini`, `PortalLogging__Level=debug` still wins; the
startup check then prints a warning that the `server.ini` value is not in effect.
Log format and file naming (`portalcore-YYYYMMDD.log`, size rollover) are unchanged.

### Home and content root are separate

| | Meaning | Development (`dotnet run`) | Standalone installation |
|---|---|---|---|
| **Portal Core home** | Installation root: `conf`, `data`, `logs`, `public`, `ai` | The project directory | The folder that contains `bin/` |
| **Content root** | ASP.NET Core content root: shipped `appsettings*.json` | The project directory | The `bin/` directory |

The content root is chosen at startup from the location of the application, **never from the working
directory**, so the shipped `appsettings.json` and `appsettings.{Environment}.json` load the same way
whether Portal Core is started from the project folder, from `/tmp`, or by a Windows service whose
working directory is `C:\Windows\System32`. `--home` and `PORTALCORE_HOME` move only the home; they do
not change the content root. `--print-config` and `--check-config` print both, on separate lines.

(Before Stage 3A.1 the content root was the working directory, so starting the DLL from another folder
silently skipped `appsettings.json` and fell back to built-in defaults.)

### Where relative paths resolve from

| Source of the path | Relative paths resolve against |
|---|---|
| `server.ini` `[paths]` | **Portal Core home** |
| `appsettings.json` (`PortalLogging:File:Directory`, `AiSettings:*Folder`) | The application content root (the project directory with `dotnet run`) |

Both are the same directory in development, so nothing moves. In a standalone installation a
`server.ini` supplies the home-relative paths. Absolute paths are used as written on Windows and Linux.

## Development versus Production

| Situation | Development | Any other environment |
|---|---|---|
| `server.ini` missing | Allowed (note shown, defaults used) | **Error**, exit code 2 |
| `server.ini` invalid | Error, exit code 2 | Error, exit code 2 |

The environment is `ASPNETCORE_ENVIRONMENT` (Production if unset). For testing without a file in
a non-Development environment, pass `--allow-missing-config`.

An invalid `server.ini` also stops normal startup (message on stderr, exit code 2) so a
misconfigured server never runs half-configured.

## Diagnostic commands

Neither command starts Kestrel, opens SQLite or starts any other service.

```bash
dotnet run --project backend/PortalCore.Api -- --check-config
dotnet run --project backend/PortalCore.Api -- --print-config
dotnet run --project backend/PortalCore.Api -- --check-config --home /path/to/home
```

- `--check-config`: validates and prints errors/warnings. Exit code `0` = valid, `2` = invalid.
- `--print-config`: shows every effective value, its source (`default`, `server.ini`,
  `environment variable`, `command line`) and the resolved absolute path of `[paths]` settings.
  Secrets are shown as `********`.
- Both can be combined and accept `--home`.

Error messages name the section and setting, and never contain secret values.
Settings whose name has the word `password`, `passphrase`, `secret` or `token`, or is `key` or ends in
`_key` (for example `api_key`), are secret: `--print-config` shows `********` and messages say `(hidden)`.
`cert_key_file` is a file path, not a secret, and is shown. `root_url` values are never echoed in errors.

## Status of each setting

| Setting | Validated | Applied to the running app |
|---|---|---|
| `[log]` (all keys) | Yes | **Yes** (Stage 2) |
| `[paths] logs` | Yes | **Yes** (Stage 2) |
| `[paths] ai` | Yes | **Yes** (Stage 2) |
| `[server] protocol`, `http_addr`, `port` | Yes | **Yes** (Stage 4), only when at least one of them is configured: Kestrel listener |
| `[tls]` | Yes | **Yes** (Stage 4), for `protocol = https` |
| `[server] domain`, `root_url` | Yes | No (validated only) |
| `[paths] data` | Yes | **Yes** (Stage 3A): SQLite database location and startup safety |
| `[paths] public` | Yes | **Yes** (Stage 5): only `config/`, `navigation/` and `content/` are served; see [standalone-hosting.md](standalone-hosting.md) |

`--print-config` marks each setting `[applied]` or `[validated only]`.

### Database location and startup safety (Stage 3A)

`[paths] data` sets the **directory**; the file name is always `portalcore.db`.

**One effective location**, highest priority first:

1. `ConnectionStrings:PortalDb` from an environment variable, the command line or user secrets
   (`ConnectionStrings__PortalDb=...`). `[paths] data` is then ignored and a warning says so.
2. `[paths] data` in `server.ini` (`<data>/portalcore.db`).
3. `ConnectionStrings:PortalDb` in `appsettings.json` (shipped default `Data Source=portalcore.db`).

A relative path is resolved against Portal Core home, never the working directory. Because the
shipped default is relative, `dotnet run` still uses `<project>/portalcore.db`, the existing file.
`--print-config` and `--check-config` show the resolved file, its state and where it came from.
Connection strings must name a file (no `:memory:` or `file:` URIs) and must not set `Mode`; values
are never printed, because a connection string can contain credentials.

**Startup behaviour** (decided by looking at the file system only; the file is never opened by the check):

| Situation | Result |
|---|---|
| Database file exists | Opened as-is. Not recreated, not migrated. |
| Missing, Production (any non-Development environment) | **Startup stops** (exit code 2), showing the resolved path. Nothing is created. |
| Missing, Development, default location | A new empty database is created (a warning is logged). |
| Missing, Development, location set explicitly (`server.ini`, environment, command line) | **Startup stops.** No replacement is created for a database that should exist. |
| Directory missing | Startup stops, in every environment. |
| File is 0 bytes, or not a SQLite file | Startup stops. A non-SQLite file is never touched. |

While running, the connection uses `Mode=ReadWrite`, so SQLite itself can never create a file.

**Creating a database deliberately:** `PortalCore --init-db` (add `--home`, or configure `[paths] data`).
It creates the schema in a missing or zero-byte file, creates at most the last directory level,
leaves an existing database untouched (it reports it and exits 0), and never starts the web server.
It is the only way to create a database outside the Development default.

Schema migrations are not implemented. An existing database is never altered by startup.

**Backup and restore (Stage 3B.1)** are implemented as command-line commands (`--backup-db`,
`--list-backups`, `--verify-backup <file>`, `--restore-db <file> [--yes]`, optional `--database <id>`).
Backups go to `[paths] backups` (default `<home>/backups`). While it runs, Portal Core holds a shared
maintenance lock on `<database>.maintenance.lock`; a restore needs it exclusively and is refused otherwise.
The lock does not stop external SQLite tools. See [backup-restore.md](backup-restore.md) for the
procedure, exit codes, limitations and manual recovery.

### Development defaults versus standalone defaults

| | Development (`dotnet run`, no `server.ini`) | Standalone installation (later stage) |
|---|---|---|
| Database | `<project>/portalcore.db` (existing file; created if missing) | `<home>/data/portalcore.db` (from `[paths] data`; never created automatically) |
| Logs | `<project>/logs` | `<home>/logs` (from `[paths] logs`) |
| AI knowledge / prompts | `<project>/Data/knowledge`, `<project>/Data/prompts` (unchanged) | `<home>/ai/knowledge`, `<home>/ai/prompts` (from `[paths] ai`) |
| Log level and rolling | `appsettings.json` | `[log]` |

`[paths] ai` has no built-in default on purpose, so existing development files are never moved or
ignored. The installation sample sets `ai = ai`; in development, leave it out.

Planned for later stages: secrets such as AI provider keys.

## Security

- `server.ini` is outside every web-served directory and is never mapped to a URL.
- Startup rejects a `[paths] public` that would contain `conf`, `data`, `logs` or `ai`.
- Restrict the `conf` folder with NTFS permissions (Administrators: Full Control; the Portal
  Core service account: Read). The application does not manage permissions.
