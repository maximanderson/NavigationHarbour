# Database Backup and Restore (administration guide)

Portal Core backs up and restores its SQLite database from the command line. None of these commands
starts the web server. Add `--home <directory>` when the home directory cannot be found automatically.

## Commands

| Command | What it does |
|---|---|
| `PortalCore --backup-db` | Takes a verified backup. Works while Portal Core is running. |
| `PortalCore --list-backups` | Lists the backups of the database, newest first. |
| `PortalCore --verify-backup <file>` | Checks a backup (checksum and SQLite integrity). Read-only. |
| `PortalCore --restore-db <file>` | Checks the restore and reports. **Changes nothing** without `--yes`. |
| `PortalCore --restore-db <file> --yes` | Replaces the live database with the backup. **Portal Core must be stopped.** |

`<file>` is a plain file name (looked up in the backup directory) or a path. `--database <id>` selects the
database when more than one is registered; today there is one (`portal`), so it can be left out.

```
PortalCore --home /opt/portalcore --backup-db
PortalCore --home /opt/portalcore --list-backups
PortalCore --home /opt/portalcore --verify-backup portal-20261115T110312Z.db
PortalCore --home /opt/portalcore --restore-db portal-20261115T110312Z.db          # check only
PortalCore --home /opt/portalcore --restore-db portal-20261115T110312Z.db --yes    # really restore
```

**Exit codes:** `0` success. `2` refused or failed (nothing was published or changed unless the message says
otherwise). A restore without `--yes` also exits `2`, because it did not restore anything.

## Backups

- Directory: `[paths] backups` (default `<home>/backups`). It may not be, or contain, the database directory,
  `conf` or `public`. Backups contain Usage Monitor data: restrict access and never commit them.
- Name: `portal-<yyyyMMdd>T<HHmmss>Z.db` plus `<name>.meta.json` (time, size, SHA-256, version, reason).
- A backup is written to a hidden `.partial` file, verified (SQLite `integrity_check`, Portal Core schema),
  and only then given its final name. A failure leaves nothing that looks like a backup.
- **Online backup:** uses SQLite's backup API on a read-only connection. It is consistent, includes committed
  data still in the WAL, never changes the database and never creates a missing one. Each backup is one
  self-contained `.db` file; never copy `portalcore.db`, `-wal` and `-shm` by hand.
- `--verify-backup` also compares the checksum with the metadata. Without a metadata file only the SQLite
  checks run.

## Restore

A restore is **offline**. Stop Portal Core (the service) first. Order of operations, all under the
maintenance lock:

1. Take the exclusive maintenance lock (refuse, changing nothing, if it is held).
2. Verify the backup (checksum, integrity, Portal Core schema).
3. Take a verified **pre-restore backup** of the current database (`reason: pre-restore`). If this fails, stop.
4. Build the replacement next to the live file (`portalcore.db.restore-<id>.partial`) and verify it. The live
   database is still untouched. All SQLite connections of the restore are closed afterwards.
5. Rename the live `.db`, `-wal`, `-shm` and `-journal` aside as `portalcore.db.restore-aside-<id>*`.
6. Rename the replacement into place. On any failure in steps 5-6 the original files are renamed back.
7. Delete the aside copies. The pre-restore backup remains.

A WAL database stays in WAL mode. The old `-wal`/`-shm` are never reused, so a stale WAL cannot be applied to
the restored file. A restore into a missing database recreates it (the last directory level may be created).
A file that is not SQLite is never overwritten.

**Not atomic.** Each rename is atomic, the sequence is not. A power loss or kill between steps 5 and 6 leaves
the database without a live file. Nothing is deleted before the new file is in place, so it is recoverable.

## Maintenance lock

- File: `<database file>.maintenance.lock` (for example `data/portalcore.db.maintenance.lock`), so it belongs
  to the effective database location, not to the working directory. Leave the file alone; it may be empty.
- Portal Core takes a **shared** lock on start-up and keeps it until it exits. A restore takes an **exclusive**
  lock and keeps it for the whole operation, including the pre-restore backup. A second Portal Core refuses to
  start while a restore runs. `--restore-db` without `--yes` only tests the lock and releases it.
- It is an operating-system file lock (share mode on Windows, `flock` on Linux), not a PID or "file exists"
  check. The system releases it when the process ends, including after a crash or `kill -9`. A lock file left
  behind is harmless.
- A restore also refuses when the system does not enforce the lock (`DOTNET_SYSTEM_IO_DISABLEFILELOCKING` set).

**Limitations. This is not complete protection.**

- It protects only against processes that use the lock: Portal Core and these commands. An **external SQLite
  client** (DB Browser, `sqlite3`, a script, another application) ignores it and can keep the database open or
  write to it during a restore. Close such tools before restoring.
- Network and some container file systems may not enforce file locks; keep the database on a local disk.
  Do not delete the lock file while Portal Core is running.
- A Portal Core build from before this lock existed does not take it. Stop all services before restoring.
- The Windows behaviour follows from .NET's documented share-mode semantics; it was not run on Windows here.

## After an interrupted restore

Symptom: the restore stopped with an error saying the original could not be put back, or the next restore
reports "A previous restore was interrupted".

1. Make sure Portal Core and other database tools are stopped.
2. In the database directory look for `portalcore.db.restore-aside-<id>` (the original database) with
   optional `-wal`, `-shm`, `-journal` suffixes, and for `portalcore.db.restore-<id>.partial`.
3. If `portalcore.db` is missing or damaged: delete any `portalcore.db`, `-wal` and `-shm` that are present,
   then rename each `...restore-aside-<id>` file back by removing `.restore-aside-<id>` from its name.
   Delete the `.partial` file.
4. If in doubt, run `--verify-backup` on the latest pre-restore backup; it is a complete copy of the original.
   You can restore it with `--restore-db <pre-restore file> --yes`.
5. If `portalcore.db` is intact and the aside files are redundant (the restore had finished), delete them.

New restores refuse to run while `restore-aside` files exist, so they are never overwritten.

