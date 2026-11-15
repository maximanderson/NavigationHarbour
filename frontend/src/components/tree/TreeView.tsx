// src/components/TreeView.tsx
import { useState } from "react";
import type { TreeNode } from "../../types/portal";
import { TreeNodeItem } from "./TreeNodeItem";

interface TreeViewProps {
  nodes: TreeNode[];
  onNodeClick?: (node: TreeNode) => void;
  /**
   * When provided, overrides the real expansion state for display purposes
   * (e.g. while a search is active) without mutating it. Clearing the
   * override (passing undefined) reveals the untouched expansion state
   * exactly as the user left it.
   */
  forcedExpandedIds?: Set<string>;
  /**
   * Ids expanded when the component first mounts. Later changes are ignored;
   * remount (change `key`) to apply a new initial state.
   */
  initialExpandedIds?: Set<string>;
}

export function TreeView({
  nodes,
  onNodeClick,
  forcedExpandedIds,
  initialExpandedIds,
}: TreeViewProps) {
  const [expandedIds, setExpandedIds] = useState<Set<string>>(
    () => new Set(initialExpandedIds)
  );

  function handleToggle(id: string) {
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }

  return (
    <div>
      {nodes.map((node) => (
        <TreeNodeItem
          key={node.id}
          node={node}
          level={0}
          onNodeClick={onNodeClick}
          expandedIds={expandedIds}
          forcedExpandedIds={forcedExpandedIds}
          onToggle={handleToggle}
        />
      ))}
    </div>
  );
}
