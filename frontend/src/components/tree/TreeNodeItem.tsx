import type { TreeNode } from "../../types/portal";

interface Props {
  node: TreeNode;
  level?: number;
  onNodeClick?: (node: TreeNode) => void;
  expandedIds: Set<string>;
  forcedExpandedIds?: Set<string>;
  onToggle: (id: string) => void;
}

export function TreeNodeItem({
  node,
  level = 0,
  onNodeClick,
  expandedIds,
  forcedExpandedIds,
  onToggle,
}: Props) {
  const hasChildren = node.children && node.children.length > 0;

  // When a search override is active, it takes precedence so matching
  // branches are visible. The user's real expansion state (expandedIds) is
  // never modified by the override, so it's preserved untouched underneath.
  const expanded = forcedExpandedIds
    ? forcedExpandedIds.has(node.id)
    : expandedIds.has(node.id);

  return (
    <div className="tree-node">
      <div className="tree-node-row">
        {hasChildren && (
          <span
            className="tree-node-toggle"
            onClick={() => onToggle(node.id)}
          >
            {expanded ? "▼" : "▶"}
          </span>
        )}

        <span
          className={`tree-node-label ${node.url ? "clickable" : ""}`}
          title={node.title}
          onClick={() => node.url && onNodeClick?.(node)}
        >
          {node.title}
        </span>
      </div>

      {expanded && hasChildren && (
        <div>
          {node.children!.map((child) => (
            <TreeNodeItem
              key={child.id}
              node={child}
              level={level + 1}
              onNodeClick={onNodeClick}
              expandedIds={expandedIds}
              forcedExpandedIds={forcedExpandedIds}
              onToggle={onToggle}
            />
          ))}
        </div>
      )}
    </div>
  );
}
