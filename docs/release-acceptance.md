# Manual acceptance test: Windows package (v1.1.0)

Repository-only checklist (it is not part of the release package). Use a **separate installation home for each
scenario**, so that one scenario can never change the starting state of another. Never test against a production
installation or a real database.

Replace `<package>` with the unpacked package folder and `<exe>` with `<home>\bin\NavigationHarbour.exe`.

| Scenario | Home | Starting state of `data\` |
|---|---|---|
| A. Clean installation | `C:\NH-Test-Clean` | empty |
| B. Legacy upgrade | `C:\NH-Test-Legacy` | only `portalcore.db` (a copy of a **test** database) |
| C. Ambiguous databases | `C:\NH-Test-Both` | both `portalcore.db` and `usage.db` |

## Preparation (for every scenario)

1. Unpack the ZIP and copy the folder `NavigationHarbour` to the scenario's home (for example `C:\NH-Test-Clean`).
   Use a fresh copy each time; do not reuse a home between scenarios.
2. Copy `conf\server.sample.ini` to `conf\server.ini`. Give each home its own `[server] port` (for example 5247, 5248,
   5249) so that homes can run at the same time without clashing.
3. Run `<exe> --home <home> --check-config` and expect `Result: OK` (scenario C: see below).

Scenario B and C need a **test** database in the legacy format. Create one by running scenario A first and copying
`C:\NH-Test-Clean\data\usage.db` (stopped) to the other home as `data\portalcore.db`. Do not use real data.

## A. Clean installation (`C:\NH-Test-Clean`)

1. `--check-config`: the database is reported as missing (it is never created automatically in production).
2. `--init-db`: creates `data\usage.db`. Running it a second time changes nothing.
3. `--backup-db`, `--list-backups`, `--verify-backup <file>`: succeed; the backup is in `backups\`.
4. Start `<exe> --home <home>`, open the portal, click a leaf item; stop the server.
5. Check: `data\` contains `usage.db` and **no** `portalcore.db`.

## B. Legacy upgrade (`C:\NH-Test-Legacy`)

Put the test database in `data\portalcore.db`; leave `usage.db` absent.

1. `--check-config` and `--print-config`: the database is `data\portalcore.db` and is described as a legacy file name.
2. `--init-db`: says the database already exists; **no** `usage.db` is created.
3. `--backup-db`, `--list-backups`, `--verify-backup`: work on `portalcore.db`.
4. `--restore-db <backup> --yes` with the server stopped: succeeds and keeps the name `portalcore.db`.
5. Start the server, open the portal, click a leaf item, stop it.
6. Check: `data\` still has only `portalcore.db` (plus `.maintenance.lock`); nothing was renamed, moved or copied.

## C. Ambiguous databases (`C:\NH-Test-Both`)

Put test databases in both `data\portalcore.db` and `data\usage.db`. Note the file sizes and timestamps first.

1. `--check-config`: `Result: INVALID`, exit code 2, message names both files.
2. Normal start, `--init-db`, `--backup-db`, `--list-backups`, `--verify-backup x`, `--restore-db x --yes`: every one
   stops with exit code 2 and "Nothing was changed".
3. Choose explicitly: set the environment variable
   `PORTAL_ConnectionStrings__PortalDb=Data Source=C:\NH-Test-Both\data\usage.db` (in the same console), then
   `--check-config` is `OK` with a warning that `[paths] data` is ignored, and `--backup-db` works.
4. Close the console (or clear the variable) and repeat step 1: it is ambiguous again.
5. Check: both files still exist with the sizes and timestamps noted at the start (apart from what step 3 legitimately
   touches: a backup file in `backups\` and a `.maintenance.lock`).

## Package checks (once)

- The ZIP's SHA-256 equals the value in the `.sha256` file.
- The package holds no `.db`, `server.ini`, certificate, key or `.pdb` file.
- `bin\appsettings.json` has the default `Data Source=usage.db`; this is expected and does not bypass legacy
  discovery (discovery applies to the shipped default, see `server-configuration.md`).
- Renaming check: the executable is `bin\NavigationHarbour.exe` and the window/page titles show "Navigation Harbour".
