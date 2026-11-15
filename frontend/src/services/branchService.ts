import type { BranchColumn } from "../types/portal";

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/**
 * Loads and validates a Branch Screen definition.
 *
 * Expected shape:
 *   [{ "title": "SAW", "branches": [{ "title": "...", "navID": "481" }] }]
 *
 * Throws a descriptive Error if the file cannot be fetched or is invalid.
 */
export async function loadBranches(source: string): Promise<BranchColumn[]> {
  if (!source) {
    throw new Error("Branch Screen source is not configured.");
  }

  const response = await fetch(source);

  if (!response.ok) {
    throw new Error(`Failed to load branches file: ${source}`);
  }

  const data: unknown = await response.json();

  if (!Array.isArray(data)) {
    throw new Error(`Branches file must contain an array: ${source}`);
  }

  return data.map((column: unknown, columnIndex: number) => {
    if (!isRecord(column) || !Array.isArray(column.branches)) {
      throw new Error(
        `Invalid branch column at index ${columnIndex} in ${source}: expected "title" and a "branches" array.`
      );
    }

    return {
      title: String(column.title ?? ""),
      rows: column.branches.map((branch: unknown, rowIndex: number) => {
        if (
          !isRecord(branch) ||
          typeof branch.title !== "string" ||
          (typeof branch.navID !== "string" && typeof branch.navID !== "number")
        ) {
          throw new Error(
            `Invalid branch at column ${columnIndex}, index ${rowIndex} in ${source}: expected "title" and "navID".`
          );
        }

        return { title: branch.title, navId: String(branch.navID) };
      }),
    };
  });
}
