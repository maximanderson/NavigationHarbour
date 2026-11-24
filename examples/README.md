# Examples

Reusable, fictional examples for Navigation Harbour. Copy them, then replace the example addresses with your own dashboards.

| File | What it is |
|---|---|
| [`navigation/example-navigation.json`](navigation/example-navigation.json) | A navigation tree: 3 branches, 4 levels, 11 dashboards |
| [`navigation/example-branches.json`](navigation/example-branches.json) | A Branch Screen: 3 columns of buttons. Every `navID` is an `id` in the navigation file |
| [`config/portal.ini`](config/portal.ini) | A `portal.ini` with two screens that load the two files above |

## Using them

| To use them with… | Copy |
|---|---|
| the **Windows package** | the two JSON files to `NavigationHarbour/public/navigation/`, and `portal.ini` to `NavigationHarbour/public/config/` (keep a copy of the one you replace) |
| **development** | the same files to `frontend/public/navigation/` and `frontend/public/config/` |

Then reload the page in your browser (Ctrl+F5 if you still see the old one). Navigation Harbour does not need a restart for these files.

## The rules in one place

**Navigation file** (a JSON array of nodes)

| Field | Required | Notes |
|---|---|---|
| `id` | yes | Non-empty string (or number), unique in the whole file |
| `title` | yes | Shown in the tree, and searched |
| `url` | no | Opened in the iframe when the item is clicked. Items without one are branches only |
| `children` | no | Array of nodes |

**Branch Screen file** (a JSON array of columns)

```json
[ { "title": "COLUMN", "branches": [ { "title": "BUTTON", "navID": "an-id-from-the-navigation-file" } ] } ]
```

**`portal.ini`**: each screen needs `[screen.<id>]`, `[screen.<id>.nav.left]` (with `type = tree` and `source = /navigation/<file>.json`) and `[screen.<id>.content]` (with `renderer = iframe`). Add `defaultRenderer = branches` and `branchesSource = /navigation/<file>.json` to the content section to start the screen with a Branch Screen. Comments go on their own line.

## Notes

- The example addresses (`grafana.example.com`, `powerbi.example.com`, `pivision.example.com`, `reports.example.com`) do not exist, so those dashboards show an error in the frame. **"Water Quality Summary (loads example.com)"** points at `https://example.com/`, which can be framed, so you can see the pane working straight away.
- A dashboard can only appear in the frame if its own system allows embedding. See "Security and iframe considerations" in the [main README](../README.md#7-security-and-iframe-considerations).
- These files are examples only. They are not part of the Windows release package, which has its own demonstration data.
