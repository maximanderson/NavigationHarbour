import type { LoggingConfig, LogLevel } from "../types/portal";

/**
 * Centralised application diagnostic logger.
 *
 * - Controlled only by the `[logging]` section of portal.ini.
 * - Controls Navigation Harbour's own output only; browser-native errors and
 *   security warnings are never suppressed.
 * - Logs stay in the browser console; nothing is sent to the backend.
 * - Before configureLogger() is called (e.g. while portal.ini is loading)
 *   the safe defaults below apply, so early failures are still reported.
 */

export const LOG_LEVELS: readonly LogLevel[] = [
  "debug",
  "info",
  "warning",
  "error",
];

export const DEFAULT_LOGGING_CONFIG: LoggingConfig = {
  enabled: true,
  level: "info",
  console: true,
};

const LEVEL_RANK: Record<LogLevel, number> = {
  debug: 0,
  info: 1,
  warning: 2,
  error: 3,
};

const PREFIX = `[${__PRODUCT_TECHNICAL_NAME__}]`;

let current: LoggingConfig = { ...DEFAULT_LOGGING_CONFIG };

export function configureLogger(config: LoggingConfig): void {
  current = { ...config };
}

function write(level: LogLevel, message: string, args: unknown[]): void {
  if (!current.enabled || !current.console) return;
  if (LEVEL_RANK[level] < LEVEL_RANK[current.level]) return;

  switch (level) {
    case "debug":
      console.debug(PREFIX, message, ...args);
      break;
    case "info":
      console.info(PREFIX, message, ...args);
      break;
    case "warning":
      console.warn(PREFIX, message, ...args);
      break;
    case "error":
      console.error(PREFIX, message, ...args);
      break;
  }
}

export const logger = {
  debug: (message: string, ...args: unknown[]) => write("debug", message, args),
  info: (message: string, ...args: unknown[]) => write("info", message, args),
  warn: (message: string, ...args: unknown[]) => write("warning", message, args),
  error: (message: string, ...args: unknown[]) => write("error", message, args),
};
