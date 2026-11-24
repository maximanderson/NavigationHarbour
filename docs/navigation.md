# Navigation Files

Navigation trees are JSON files under `frontend/public/navigation/` and are referenced from
`portal.ini` via `source = /navigation/<file>.json` in a `[screen.<id>.nav.*]` section.

## Format

A file is a JSON array of nodes:

```json
[
  {
    "id": "7c1a9f52-3b0e-4d3c-9a57-2f1d6b8e4a10",
    "title": "Area - Northvale",
    "url": "https://example/display",
    "children": [ { "id": "…", "title": "…" } ]
  }
]
```

| Field      | Required | Notes |
|------------|----------|-------|
| `id`       | yes      | Unique node identifier (see below). |
| `title`    | yes      | Display text; searched by the navigation search. |
| `url`      | no       | Only nodes with a `url` are clickable and logged by the Usage Monitor. |
| `children` | no       | Array of nodes. |

## Node ID convention: IDs are strings

`TreeNode.id` is a **string** everywhere in Navigation Harbour (frontend types, tree expansion, search,
branch lookup, Usage Monitor `nodeId`, and the backend `UsageLog.NodeId` column).

- **Preferred form:** a string. Generated navigation (for example from AVEVA PI AF) should use the
  AF Element GUID as the `id`. GUIDs and any other string ids are used **exactly as written**:
  they are not trimmed, lower-cased or otherwise modified.
- **Legacy numeric ids** (for example `"id": 1065` in an older numeric navigation file)
  remain supported. They are converted to strings (`1065` → `"1065"`) once, when the file is
  loaded by `loadNavigation()` in `frontend/src/services/navigationService.ts`. No other code needs
  to handle numbers.
- **Rejected:** a missing `id`, `null`, an empty or whitespace-only string, `NaN`/infinite numbers,
  booleans, objects and arrays. The whole file fails to load and the navigation panel shows an
  error that names the node position, its title (if any) and the file, e.g.
  `Invalid navigation node at root[3].children[1] ("Pump") in /navigation/x.json: missing id; "id" must be a non-empty string or a finite number.`
- **Ids must be unique within a file.** Uniqueness is checked after normalization, over the whole
  tree at every depth, so a numeric `5` and a string `"5"` count as duplicates. A duplicate makes
  the file fail to load, and the error gives the id and both locations (first use, then the
  duplicate), e.g.
  `Duplicate navigation node id "5" in /navigation/x.json: used at root[0] ("A") and again at root[1].children[0] ("B").`
  The same id may appear in **different** files; each file is validated on its own.
- Because comparison is exact, a numeric id `1` and the string `"1"` are the same id after
  loading, but a GUID written in different letter case is a different id (and is not reported as
  a duplicate). Generators should emit GUIDs in one consistent case.

## Branch Screen `navID`

`navID` values in a branches file (for example `demo-branches.json`) may be strings or
numbers. They are converted to strings by `branchService.ts` and matched against the normalized
node ids, so `"navID": 474` and `"navID": "474"` both select node `"474"`.

## Usage Monitor

When a node with a `url` is clicked, `POST /api/usage` receives `nodeId` as the node's string id.
Numeric JSON values for `nodeId` are not accepted by the backend; the frontend normalization
guarantees a string is always sent.
