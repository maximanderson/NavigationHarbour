# Portal Core release package (Windows x64)

A ready-to-run demonstration package, `PortalCore-win-x64-vX.Y.Z.zip`. It is self-contained: no .NET, Node.js,
Visual Studio or IIS is needed on the PC. Everything in it is fictional demonstration data ("Aquaville").

## 1. Download from a GitHub Release

1. Open the repository on GitHub, then **Releases**, and choose the version.
2. Download two files: `PortalCore-win-x64-vX.Y.Z.zip` and `PortalCore-win-x64-vX.Y.Z.zip.sha256`.
   (If the repository or release is access-restricted, you need a signed-in account with access. Whether a release may be downloaded to a work PC is
   decided by your organisation's information-security rules, not by the repository's visibility setting.)

### Verify the download

```powershell
(Get-FileHash .\PortalCore-win-x64-vX.Y.Z.zip -Algorithm SHA256).Hash.ToLower()
Get-Content .\PortalCore-win-x64-vX.Y.Z.zip.sha256
```

The two hash values must be identical. If not, download again.

## 2. Extract

Extract the whole ZIP (right-click > Extract All) to a short path without spaces, for example `C:\`. This gives
`C:\PortalCore\`. Do not run it from inside the ZIP. If Windows marks the download as blocked, right-click the ZIP >
Properties > Unblock *before* extracting (if your policy allows).

## 3. Create `conf\server.ini`

```powershell
cd C:\PortalCore
Copy-Item conf\server.sample.ini conf\server.ini
```

The sample works as is for a first test: HTTP on `127.0.0.1:5247`. `server.ini` is private and is never part of a
package. Edit only `server.ini`, never the sample. Settings are explained in `server-configuration.md`.

## 4. Initialise the database

```powershell
.\bin\PortalCore.Api.exe --home C:\PortalCore --init-db
```

This creates `data\portalcore.db` (empty). It never touches an existing database.

## 5. Start interactively

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
.\bin\PortalCore.Api.exe --home C:\PortalCore
```

Open `http://127.0.0.1:5247/`. Stop with Ctrl+C. Check the demonstration:

- **Operations:** expand the tree, and search for `pump` or `flow` (three characters or more).
- **Regions:** choose a region or plant; the tree narrows to it. "Restore menu" shows the whole tree again.
- **Reports:** a static page. Clicking a leaf in the tree writes a Usage Monitor row to `data\portalcore.db`.
- The content area is empty by design: nothing is loaded into frames, and nothing calls an external service.
  The AI assistant is switched off in the demonstration configuration.

## 6. Install the service manually with NSSM

> **Note:** Windows service deployment has not yet been fully validated for this pre-release. Test it first on a
> non-production machine.

NSSM (nssm.cc) is a separate download and is not included. Use its window, as an Administrator:

1. Run `nssm install PortalCore` (the install window opens).
2. **Application** tab: Path `C:\PortalCore\bin\PortalCore.Api.exe`, Startup directory `C:\PortalCore\bin`,
   Arguments `--home C:\PortalCore`.
3. **Details** tab: Display name `Portal Core`, Startup type `Automatic (Delayed Start)`.
4. **Environment** tab: `ASPNETCORE_ENVIRONMENT=Production`.
5. **I/O** tab: Output (stdout) `C:\PortalCore\logs\nssm-stdout.log`, Error (stderr) `C:\PortalCore\logs\nssm-stderr.log`.
6. **Shutdown** tab: leave Ctrl+C enabled and set the timeouts to 20000 ms.
7. **Exit actions** tab: Default action `Restart`, restart delay 5000 ms, throttle 10000 ms.
8. **Log on** tab: use a dedicated low-privilege account if you can. It needs Read on `bin\`, `conf\`, `public\`,
   `certs\` and `ai\`, and Modify on `data\`, `logs\` and `backups\`.
9. Click *Install service*, then `nssm start PortalCore` (or start it in `services.msc`).

More detail, and the nightly backup job, are in `windows-deployment.md`.

## 7. Test HTTP and HTTPS

**HTTP:** browse to `http://127.0.0.1:5247/`. To reach it from another PC, set `http_addr = 0.0.0.0` (all interfaces)
in `server.ini`, allow the port in Windows Firewall, and restart. Plain HTTP is unencrypted.

**HTTPS:** put your certificate in `certs\` and set, in `server.ini`:

```ini
[server]
protocol = https
port = 5248

[tls]
cert_file = certs/portalcore.pfx
cert_password = <your password>
```

(or `cert_file` and `cert_key_file` for PEM). Run `--check-config`, then start. A self-signed certificate works but
the browser warns; use a certificate your browsers trust for real use. A bad or missing certificate stops startup:
Portal Core never falls back to HTTP.

## 8. Troubleshooting

| Symptom | What to check |
|---|---|
| Nothing starts, "server.ini not found" | Step 3. The environment must be `Production`. |
| "database ... missing" | Step 4 (`--init-db`). |
| Port already in use | Change `port` in `server.ini`; check with `netstat -ano`. |
| Page loads but is blank | Open `/config/portal.ini` and `/navigation/demo-navigation.json` in the browser; both must show text. |
| Usage is not recorded | `data\` must be writable by the account that runs Portal Core. |
| Service stops immediately | Read `logs\nssm-stderr.log`; run interactively with the same arguments. |
| Antivirus or SmartScreen blocks the exe | The files are unsigned. Verify the SHA-256 (section 1) and follow your IT policy. |

To remove it: stop the service (`nssm remove PortalCore confirm`), delete the folder. Keep `backups\` if you need it.

## What is, and is not, in this package

In it: the self-contained application, the compiled portal, demonstration configuration, navigation, Branch Screen and
report page, one fictional AI sample file, `server.sample.ini`, and these guides. **Not** in it: any database, a
`server.ini`, certificates or keys, symbol (`.pdb`) files, real navigation or operational data. The build refuses to
produce a package if its safety scan finds such material (`scripts/release-scan-rules.txt`).
