# Windows Deployment Guide

Navigation Harbour runs as one self-contained process on Windows x64: no IIS and no separately installed .NET runtime.
The same executable runs interactively and as a service under NSSM. Windows steps below were written on Linux and
must be verified on the server (marked **verify**). Running as a Windows service under NSSM has **not yet been
fully validated**; treat section 5 as untested guidance.

## 1. Build the package

On the build machine (Node.js and the .NET 10 SDK installed):

```powershell
scripts\build-standalone.ps1 -Windows -Out C:\build\NavigationHarbour       # or: scripts/build-standalone.sh --windows <dir>
```

The result is a complete Navigation Harbour home. It contains no database, no `server.ini` and no development files.

```text
NavigationHarbour\
├── bin\       NavigationHarbour.exe, wwwroot\ (React app), runtime and libraries
├── conf\      server.sample.ini
├── public\    config\portal.ini (same-origin API), navigation\, content\
├── data\  logs\  backups\  certs\  ai\ (sample knowledge and prompts)  docs\
```

Copy the folder to the server, for example `C:\NavigationHarbour`. When upgrading, replace only `bin\`; `conf\`, `public\`,
`data\`, `backups\`, `certs\` and `ai\` hold your settings and data (the script never overwrites an existing `public`).

### Upgrading from a release named Portal Core (before 1.1.0)

- **Executable.** `bin\PortalCore.Api.exe` is now `bin\NavigationHarbour.exe`. Stop the service, replace `bin\`, then point the
  service at the new file: `nssm set <service name> Application C:\NavigationHarbour\bin\NavigationHarbour.exe`. Update any
  scheduled task or script that calls the old executable name (for example the nightly backup).
- **Database.** An existing `data\portalcore.db` keeps being used; it is not renamed or copied. New installations use `data\usage.db`.
  If both files exist, startup stops until you choose one (see "Legacy database name" in `server-configuration.md`).
- **Environment variables.** `PORTALCORE_*` and `PORTALCORE_HOME` still work; `PORTAL_*` and `PORTAL_HOME` take precedence.
- **Logs.** New log files are `logs\application-*.log`. Old `portalcore-*.log` files stay and are no longer deleted automatically;
  update any log collector or monitoring rule (see `logging.md`).

## 2. Configure

1. Copy `conf\server.sample.ini` to `conf\server.ini` and edit it (port, `http_addr`, `[paths]`, `[tls]`).
   Restrict `conf\` and `certs\` to Administrators and the service account (Read).
2. `public\config\portal.ini` is already standalone: it has no `host` and `port` for `[usage_monitor]` and `[ai]`,
   so the portal calls `/api/...` on the server it was loaded from (HTTP or HTTPS). Edit titles and screens here.
3. Check it: `bin\NavigationHarbour.exe --home C:\NavigationHarbour --check-config`
   Details: `server-configuration.md`. HTTPS certificates go in `certs\` and `[tls]`.
4. Behind IIS (ARR) or another reverse proxy? See `reverse-proxy.md` so that the real client address is recorded.
   A same-host IIS ARR deployment has been manually validated; other proxies are documented but not manually validated.

## 3. Create or move the database

New installation:

```powershell
bin\NavigationHarbour.exe --home C:\NavigationHarbour --init-db
```

Existing installation: do not copy `usage.db` by hand. On the old server run `--backup-db`, copy the backup
file (and its `.meta.json`) to the new `backups\`, then:

```powershell
bin\NavigationHarbour.exe --home C:\NavigationHarbour --restore-db portal-YYYYMMDDTHHMMSSZ.db --yes
```

(Stop the old service first if you want the very latest data.) See `backup-restore.md`.

## 4. Run interactively

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
bin\NavigationHarbour.exe --home C:\NavigationHarbour
```

Open the address from `[server]` (default `http://127.0.0.1:5247/`). Stop with Ctrl+C.
`--home` makes the result independent of the current directory.

## 5. Run as a service with NSSM

NSSM is a separate download (nssm.cc); Navigation Harbour contains no service code. From an Administrator prompt:

```powershell
nssm install NavigationHarbour C:\NavigationHarbour\bin\NavigationHarbour.exe
nssm set NavigationHarbour AppParameters "--home C:\NavigationHarbour"
nssm set NavigationHarbour AppDirectory C:\NavigationHarbour\bin
nssm set NavigationHarbour AppEnvironmentExtra ASPNETCORE_ENVIRONMENT=Production
nssm set NavigationHarbour Start SERVICE_DELAYED_AUTO_START
nssm set NavigationHarbour AppStopMethodConsole 20000
nssm set NavigationHarbour AppStdout C:\NavigationHarbour\logs\nssm-stdout.log
nssm set NavigationHarbour AppStderr C:\NavigationHarbour\logs\nssm-stderr.log
nssm set NavigationHarbour AppRotateFiles 1
nssm set NavigationHarbour AppRotateBytes 5000000
nssm set NavigationHarbour AppExit Default Restart
nssm set NavigationHarbour AppThrottle 10000
nssm set NavigationHarbour AppRestartDelay 5000
nssm start NavigationHarbour
```

- **Account:** use a dedicated low-privilege account (or `LocalService`), not an administrator:
  `nssm set NavigationHarbour ObjectName .\svc-navigationharbour <password>`. It needs Read on `bin\`, `conf\`, `certs\`, `public\`
  and `ai\`, and Modify on `data\`, `logs\` and `backups\`.
- **Stopping:** NSSM sends Ctrl+C and waits 20 seconds, which lets Kestrel and SQLite close cleanly (this releases the
  maintenance lock; a restore needs the service stopped).
- **Logs:** the application writes `logs\application-*.log` (see `logging.md`). The two `nssm-*.log` files only catch
  start-up failures before logging starts.
- **Bad configuration:** exit code 2 means invalid configuration or certificate. NSSM keeps retrying every
  10 seconds; read `nssm-stderr.log`, fix `server.ini`, then restart.

## 6. Nightly backup (Task Scheduler)

`--backup-db` works while the service runs, so stopping the service is optional. To stop it first, as planned:

```powershell
# C:\NavigationHarbour\nightly-backup.ps1  (run daily by Task Scheduler as the service account or an administrator)
$ErrorActionPreference = "Stop"
$log = "C:\NavigationHarbour\logs\backup-job.log"
try {
  Stop-Service NavigationHarbour
  (Get-Service NavigationHarbour).WaitForStatus("Stopped", "00:01:00")
  & C:\NavigationHarbour\bin\NavigationHarbour.exe --home C:\NavigationHarbour --backup-db *>> $log
  if ($LASTEXITCODE -ne 0) { throw "backup failed with exit code $LASTEXITCODE" }
}
finally {
  Start-Service NavigationHarbour
}
```

Old backups are not removed automatically. Delete old files from `backups\` yourself (for example with a
`Get-ChildItem ... | Where LastWriteTime -lt (Get-Date).AddDays(-30) | Remove-Item` line in the same job).

## 7. Troubleshooting

| Symptom | Check |
|---|---|
| Service stops at once | `logs\nssm-stderr.log`; run interactively with the same arguments; `--check-config` |
| "server.ini not found" | Copy `server.sample.ini` to `server.ini`; the environment must be `Production` |
| "database ... missing" | Run `--init-db`, or `--restore-db` |
| "maintenance lock ... held" on restore | The service is still running: stop it and wait |
| HTTPS does not start | Exit code 2 with a message about the certificate; check `[tls]` paths and password |
| Page loads but API calls fail | Open `/config/portal.ini`: `[usage_monitor]` and `[ai]` must have no `host` |
| 404 for the portal at `/` | `bin\wwwroot\index.html` is missing: rebuild the package |
| Grafana frames stay empty | Grafana must allow embedding (its `X-Frame-Options` / CSP) and, with HTTPS, be HTTPS too |
