import type { TreeNode } from "../types/portal";

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/**
 * Converts a raw `id` from a navigation file to a string.
 *
 * - Non-empty strings (e.g. AF Element GUIDs) are returned unchanged.
 * - Finite numbers (legacy files, e.g. v16/v17) are converted with String().
 * - Anything else (missing, null, empty/blank, boolean, object, NaN, ...)
 *   is rejected.
 */
function normalizeId(rawId: unknown): string | null {
  if (typeof rawId === "string" && rawId.trim() !== "") {
    return rawId;
  }

  if (typeof rawId === "number" && Number.isFinite(rawId)) {
    return String(rawId);
  }

  return null;
}

function describeId(rawId: unknown): string {
  if (rawId === undefined) {
    return "missing id";
  }

  return `invalid id ${JSON.stringify(rawId) ?? String(rawId)}`;
}

/**
 * Recursively validates raw navigation nodes and returns copies whose `id`
 * (and every descendant's `id`) is a string.
 *
 * `seenIds` maps each normalized id to the location of the first node that
 * used it, so duplicates within one tree are rejected. It is created per
 * loaded file, so the same id may appear in different navigation files.
 */
function normalizeNodes(
  rawNodes: unknown[],
  source: string,
  parentPath: string,
  seenIds: Map<string, string>
): TreeNode[] {
  return rawNodes.map((rawNode, index) => {
    const location = `${parentPath}[${index}]`;

    if (!isRecord(rawNode)) {
      throw new Error(
        `Invalid navigation node at ${location} in ${source}: expected an object.`
      );
    }

    const title =
      typeof rawNode.title === "string" ? ` ("${rawNode.title}")` : "";

    const id = normalizeId(rawNode.id);

    if (id === null) {
      throw new Error(
        `Invalid navigation node at ${location}${title} in ${source}: ${describeId(
          rawNode.id
        )}; "id" must be a non-empty string or a finite number.`
      );
    }

    const firstLocation = seenIds.get(id);

    if (firstLocation !== undefined) {
      throw new Error(
        `Duplicate navigation node id "${id}" in ${source}: used at ${firstLocation} and again at ${location}${title}.`
      );
    }

    seenIds.set(id, `${location}${title}`);

    const node = { ...rawNode, id } as unknown as TreeNode;

    if (rawNode.children !== undefined) {
      if (!Array.isArray(rawNode.children)) {
        throw new Error(
          `Invalid navigation node at ${location}${title} in ${source}: "children" must be an array.`
        );
      }

      node.children = normalizeNodes(
        rawNode.children,
        source,
        `${location}.children`,
        seenIds
      );
    }

    return node;
  });
}

export async function loadNavigation(source: string): Promise<TreeNode[]> {
  if (!source) {
    return [];
  }

  const response = await fetch(source);

  if (!response.ok) {
    throw new Error(`Failed to load navigation file: ${source}`);
  }

  const data = await response.json();

  if (!Array.isArray(data)) {
    throw new Error(`Navigation file must contain an array: ${source}`);
  }

  // Boundary normalization: source files may use numeric ids; everything
  // downstream of this point can rely on TreeNode.id being a string. Ids must
  // be unique within this file (checked after normalization).
  return normalizeNodes(data, source, "root", new Map<string, string>());
}