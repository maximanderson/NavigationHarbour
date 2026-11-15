# System Logging

Portal Core has three independent concerns:

| Concern | Purpose | Configured by |
|---|---|---|
| **System Logging** (this document) | Application diagnostics and errors | `[logging]` in `portal.ini`, `PortalLogging` in `appsettings.json` |
| **Usage Monitor** | SQLite-based portal usage tracking | `[usage_monitor]` in `portal.ini` |
| **AI Analysis** | Future analysis of AI requests/responses | Not implemented; designed separately |

The three are independent. Disabling system logging does **not** affect the Usage Monitor
(and vice versa). System logs never contain AI prompts or responses; any future AI Analysis
must use its own storage and must not write into the system logs.

## Levels

`debug` < `info` < `warning` < `error`. The level is a threshold: messages at or above it are written.
On the backend, `error` also includes ASP.NET Core's `Critical` severity.

## Frontend (`frontend/public/config/portal.ini`)

```ini
[logging]
enabled = true
level = info
console = true
```

- `enabled = false` turns off all Portal Core diagnostic logging.
- `console = false` turns off browser console output from Portal Core's logger.
- Invalid values fall back to the defaults above and a warning is logged.
- Logs stay in the browser console; nothing is sent to the backend.
- Only Portal Core's own logger is controlled. Browser-native errors and security warnings are never suppressed.
- If `portal.ini` itself cannot be loaded, the logger uses its defaults and the error is still
  shown in the UI regardless of logging settings.
- Use `logger` from `src/services/logger.ts` instead of `console.*`.

## Backend (`backend/PortalCore.Api/appsettings.json`)

```json
"PortalLogging": {
  "Enabled": true,
  "Level": "info",
  "Console": true,
  "File": {
    "Enabled": true,
    "Directory": "logs",
    "MaxFileSizeMb": 10,
    "RetainDays": 14
  }
}
```

| Key | Default | Notes |
|---|---|---|
| `Enabled` | `true` | `false` disables all diagnostic output (console and file). |
| `Level` | `info` | `debug`, `info`, `warning`, `error`. |
| `Console` | `true` | Console destination. |
| `File:Enabled` | `true` | File destination, independent of `Console`. |
| `File:Directory` | `logs` | Relative to the application content root (or absolute). |
| `File:MaxFileSizeMb` | `10` | A new file (`_001`, `_002`, …) starts when exceeded. |
| `File:RetainDays` | `14` | Older log files are deleted. |

Invalid or missing values fall back to the defaults and a warning is written at startup.
Values can be overridden through environment variables, e.g. `PortalLogging__Level=debug`.

### Log files

- Written by [Serilog](https://serilog.net/) (`Serilog.AspNetCore`) to `<directory>/portalcore-YYYYMMDD.log`.
- Rotation: daily, and when `MaxFileSizeMb` is exceeded. Retention: `RetainDays`.
- `logs/` is excluded from Git. A directory inside the public web root (`wwwroot`) is refused and
  the default `logs` directory is used instead.
- If the log file cannot be written (permissions, disk full), the portal keeps running; Serilog
  reports the problem on stderr. If the directory cannot be created, file logging is disabled
  and the console destination is still used.

### Relationship to the standard `Logging` section

Serilog replaces the default ASP.NET Core logging providers, so messages are written once and the
legacy `Logging:LogLevel` section no longer has any effect. `PortalLogging` is the single source of
truth. Framework namespaces (`Microsoft`, `System`) are limited to `warning` or the configured level,
whichever is higher.

### Error handling

Unhandled exceptions are logged once (with stack trace, in the log only) and the client receives
`500 { "error": "An unexpected error occurred.", "traceId": "…" }` — no stack trace or exception message.
Use the `traceId` to find the entry in the log.

## Privacy

System logs must not contain API keys, credentials or tokens, AI prompts or responses, retrieved
knowledge content, raw provider response bodies, or URLs with sensitive query strings. Services log
only operational data: lengths, counts, durations, status codes and success/failure.
