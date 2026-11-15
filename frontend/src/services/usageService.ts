import type {
  TreeNode,
  UsageMonitorConfig,
} from "../types/portal";
import { buildApiUrl } from "../utils/urlBuilder";

interface UsageLogRequest {
  screenId: string;
  nodeId: string;
  title: string;
  url?: string;
}

export async function logUsage(
  config: UsageMonitorConfig,
  request: UsageLogRequest
): Promise<void> {
  if (!config.enabled) {
    return;
  }

  await fetch(buildApiUrl(config), {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
    },
    body: JSON.stringify(request),
  });
}

export function buildUsageLogRequest(
  screenId: string,
  node: TreeNode
): UsageLogRequest {
  return {
    screenId,
    nodeId: node.id,
    title: node.title,
    url: node.url,
  };
}
