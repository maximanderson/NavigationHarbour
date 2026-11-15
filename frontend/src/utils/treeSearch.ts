// src/utils/treeSearch.ts
import type { TreeNode } from "../types/portal";

export type SearchMatcher = (value: string) => boolean;

const WILDCARD = "*";

/**
 * Number of characters in the query that count towards the minimum search
 * length. Wildcard characters are excluded.
 */
export function countSearchChars(query: string): number {
  return query.split(WILDCARD).join("").length;
}

/**
 * Compiles a search query into a reusable matcher (once per search).
 *
 * - No wildcard: case-insensitive substring match.
 * - With `*`: matches zero or more characters (including spaces). All other
 *   regex special characters are escaped and treated literally. The pattern
 *   is unanchored, so it matches anywhere in the value.
 */
export function compileMatcher(query: string): SearchMatcher {
  if (!query.includes(WILDCARD)) {
    const normalizedQuery = query.toLowerCase();
    return (value) => value.toLowerCase().includes(normalizedQuery);
  }

  const source = query
    .split(WILDCARD)
    .map((part) => part.replace(/[.+?^${}()|[\]\\]/g, "\\$&"))
    .join(".*");
  const regex = new RegExp(source, "i");

  return (value) => regex.test(value);
}

/**
 * Checks whether a single node matches the given query (string or
 * precompiled matcher) across the configured attributes.
 *
 * Only `title` is a known attribute on TreeNode today; other attribute names
 * are looked up dynamically.
 */
export function nodeMatches(
  node: TreeNode,
  query: string | SearchMatcher,
  attributes: string[]
): boolean {
  if (!query) {
    return false;
  }

  const matcher = typeof query === "string" ? compileMatcher(query) : query;

  for (const attribute of attributes) {
    const value = (node as unknown as Record<string, unknown>)[attribute];

    if (typeof value === "string" && matcher(value)) {
      return true;
    }
  }

  return false;
}

/**
 * Recursively filters a navigation tree against a search query.
 *
 * Rules:
 * 1. If a node matches, the original node (and its full, untouched subtree)
 *    is returned by reference.
 * 2. Otherwise, children are searched recursively. If any children match, a
 *    shallow-cloned parent is returned containing only the matching children.
 * 3. Nodes with no matches anywhere in their subtree are excluded.
 *
 * The query is compiled once and reused for the whole recursion.
 */
export function filterTree(
  nodes: TreeNode[],
  query: string | SearchMatcher,
  attributes: string[]
): TreeNode[] {
  const matcher = typeof query === "string" ? compileMatcher(query) : query;
  const result: TreeNode[] = [];

  for (const node of nodes) {
    if (nodeMatches(node, matcher, attributes)) {
      result.push(node);
      continue;
    }

    if (node.children && node.children.length > 0) {
      const filteredChildren = filterTree(node.children, matcher, attributes);

      if (filteredChildren.length > 0) {
        result.push({
          ...node,
          children: filteredChildren,
        });
      }
    }
  }

  return result;
}

/**
 * Walks an already-filtered tree and collects the ids of every node present
 * in it. Used to build the "forced expanded" set during an active search, so
 * that ancestor branches containing a matching descendant are automatically
 * opened without touching the user's real/persistent expansion state.
 */
export function collectMatchIds(filteredNodes: TreeNode[]): Set<string> {
  const ids = new Set<string>();

  function walk(nodes: TreeNode[]) {
    for (const node of nodes) {
      ids.add(node.id);

      if (node.children && node.children.length > 0) {
        walk(node.children);
      }
    }
  }

  walk(filteredNodes);

  return ids;
}

/**
 * Finds a node by id anywhere in the tree. Ids are strings (numeric ids in
 * navigation files are normalized by `loadNavigation`) and are compared
 * exactly.
 */
export function findNodeById(
  nodes: TreeNode[],
  id: string
): TreeNode | undefined {
  for (const node of nodes) {
    if (node.id === id) {
      return node;
    }

    if (node.children && node.children.length > 0) {
      const found = findNodeById(node.children, id);

      if (found) {
        return found;
      }
    }
  }

  return undefined;
}
