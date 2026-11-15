import { useEffect, useMemo, useState } from "react";
import type { NavigationPanelConfig, TreeNode } from "../types/portal";
import { loadNavigation } from "../services/navigationService";
import { TreeView } from "../components/tree/TreeView";
import { filterTree, collectMatchIds, countSearchChars, findNodeById } from "../utils/treeSearch";

interface NavigationPanelProps {
  config: NavigationPanelConfig;
  onNodeClick?: (node: TreeNode) => void;
  /** When set, only this node and its descendants are shown as the tree root. */
  rootId?: string | null;
  /** Called with the full, unmodified tree each time it finishes loading. */
  onLoaded?: (nodes: TreeNode[]) => void;
}

const SEARCH_DEBOUNCE_MS = 200;

export function NavigationPanel({
  config,
  onNodeClick,
  rootId,
  onLoaded,
}: NavigationPanelProps) {
  const [nodes, setNodes] = useState<TreeNode[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string>("");

  const [searchText, setSearchText] = useState("");
  const [debouncedSearchText, setDebouncedSearchText] = useState("");

  useEffect(() => {
    setLoading(true);
    setError("");
    loadNavigation(config.source)
      .then((data) => {
        setNodes(data);
        onLoaded?.(data);
        setLoading(false);
      })
      .catch((err) => {
        setError(err.message);
        setLoading(false);
      });
  }, [config.source, onLoaded]);

  // When the selected root changes, clear the visible and debounced search
  // text. State is adjusted during render (React-recommended pattern) so the
  // tree never renders a frame with a stale query against the new root.
  const [previousRootId, setPreviousRootId] = useState(rootId);

  if (previousRootId !== rootId) {
    setPreviousRootId(rootId);
    setSearchText("");
    setDebouncedSearchText("");
  }

  // Debounce search text so filtering a large tree doesn't run on every
  // keystroke.
  useEffect(() => {
    const handle = setTimeout(() => {
      setDebouncedSearchText(searchText);
    }, SEARCH_DEBOUNCE_MS);

    return () => clearTimeout(handle);
  }, [searchText]);

  const searchEnabled = config.search === true;
  const minChars = config.searchMinChars ?? 3;
  const searchAttributes = config.searchAttributes ?? ["title"];

  const trimmedQuery = debouncedSearchText.trim();
  const isSearchActive = searchEnabled && countSearchChars(trimmedQuery) >= minChars;

  // The tree being displayed/searched: the full tree, or the selected
  // subtree. The original `nodes` array is never modified.
  const rootNode = useMemo(
    () => (rootId ? findNodeById(nodes, rootId) : undefined),
    [nodes, rootId]
  );

  const baseNodes = useMemo(
    () => (rootNode ? [rootNode] : nodes),
    [nodes, rootNode]
  );

  // Expand the selected root one level (its immediate children visible).
  const initialExpandedIds = useMemo(
    () => (rootNode ? new Set([rootNode.id]) : undefined),
    [rootNode]
  );

  const displayedNodes = useMemo(() => {
    if (!isSearchActive) {
      return baseNodes;
    }

    return filterTree(baseNodes, trimmedQuery, searchAttributes);
  }, [baseNodes, isSearchActive, trimmedQuery, searchAttributes]);

  const forcedExpandedIds = useMemo(() => {
    if (!isSearchActive) {
      return undefined;
    }

    return collectMatchIds(displayedNodes);
  }, [isSearchActive, displayedNodes]);

  const noResults = isSearchActive && displayedNodes.length === 0;

  return (
    <div className="navigation-panel">
      <div className="navigation-panel-header">{config.title}</div>

      {searchEnabled && (
        <div className="navigation-search">
          <input
            type="text"
            className="navigation-search-input"
            placeholder="Search navigation (* = wildcard)"
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
          />
          {searchText.length > 0 && (
            <button
              type="button"
              className="navigation-search-clear"
              aria-label="Clear search"
              onClick={() => setSearchText("")}
            >
              ×
            </button>
          )}
        </div>
      )}

      <div className="navigation-panel-content">
        {loading && !error && (
          <div className="navigation-loading">Loading navigation…</div>
        )}

        {error && <div className="navigation-error">{error}</div>}

        {!loading && !error && config.type === "tree" && noResults && (
          <div className="navigation-search-empty">No matching items</div>
        )}

        {!loading && !error && config.type === "tree" && !noResults && (
          <TreeView
            key={rootId ?? "all"}
            nodes={displayedNodes}
            initialExpandedIds={initialExpandedIds}
            onNodeClick={onNodeClick}
            forcedExpandedIds={forcedExpandedIds}
          />
        )}

        {!loading && !error && (config.type === "list" || config.type === "tabs" || config.type === "buttons") && (
          <div className="navigation-not-implemented">
            Navigation type "{config.type}" is not yet implemented.
          </div>
        )}

        {!loading && !error && config.type !== "tree" && config.type !== "list" && config.type !== "tabs" && config.type !== "buttons" && (
          <div className="navigation-error">
            Unknown navigation type: "{config.type}"
          </div>
        )}
      </div>
    </div>
  );
}
