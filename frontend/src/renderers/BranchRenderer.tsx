import { useEffect, useRef, useState } from "react";
import type { BranchColumn } from "../types/portal";
import { loadBranches } from "../services/branchService";
import { logger } from "../services/logger";

interface BranchRendererProps {
  source: string;
  onSelect: (navId: string) => void;
  /** Called when the branches file cannot be loaded or is invalid. */
  onError?: (message: string) => void;
}

export function BranchRenderer({
  source,
  onSelect,
  onError,
}: BranchRendererProps) {
  const [columns, setColumns] = useState<BranchColumn[]>([]);
  const onErrorRef = useRef(onError);
  onErrorRef.current = onError;

  useEffect(() => {
    let cancelled = false;

    loadBranches(source)
      .then((data) => {
        if (!cancelled) setColumns(data);
      })
      .catch((err) => {
        if (cancelled) return;
        logger.error("Branch Screen could not be loaded:", err);
        onErrorRef.current?.(String(err.message ?? err));
      });

    return () => {
      cancelled = true;
    };
  }, [source]);

  return (
    <div className="renderer branch-renderer">
      {columns.map((column, columnIndex) => (
        <div className="branch-column" key={`${column.title}-${columnIndex}`}>
          <div className="branch-column-title">{column.title}</div>

          {column.rows.map((row, rowIndex) => {
            return (
              <button
                type="button"
                key={`${row.navId}-${rowIndex}`}
                className="branch-row"
                title={row.title}
                onClick={() => onSelect(row.navId)}
              >
                {row.title}
              </button>
            );
          })}
        </div>
      ))}
    </div>
  );
}
