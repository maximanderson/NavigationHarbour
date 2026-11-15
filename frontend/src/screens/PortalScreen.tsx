import { useState } from "react";
import { findNodeById } from "../utils/treeSearch";
import { BranchRenderer } from "../renderers/BranchRenderer";
import type {
  PortalScreenConfig,
  TreeNode,
  UsageMonitorConfig,
  AiConfig,
} from "../types/portal";
import { NavigationPanel } from "../navigation/NavigationPanel";
import { ContentRenderer } from "../renderers/ContentRenderer";
import { buildUsageLogRequest, logUsage } from "../services/usageService";
import { logger } from "../services/logger";

interface PortalScreenProps {
  screenId: string;
  config: PortalScreenConfig;
  usageMonitor: UsageMonitorConfig;
  aiConfig: AiConfig;
}

function getInitialUrl(config: PortalScreenConfig): string {
  if (config.content.renderer === "iframe") {
    return config.content.defaultUrl;
  }

  return "";
}

function getBranchesSource(config: PortalScreenConfig): string {
  if (
    config.content.renderer === "iframe" &&
    config.content.defaultRenderer === "branches" &&
    config.content.branchesSource
  ) {
    return config.content.branchesSource;
  }

  return "";
}

export function PortalScreen({
  screenId,
  config,
  usageMonitor,
  aiConfig,
}: PortalScreenProps) {
  const [currentUrl, setCurrentUrl] = useState(() => getInitialUrl(config));
  // Incremented on every navigation-item click so the iframe reloads even
  // when the URL is identical to the current one.
  const [reloadKey, setReloadKey] = useState(0);

  const branchesSource = getBranchesSource(config);
  const [showBranches, setShowBranches] = useState(branchesSource !== "");
  const [branchRootId, setBranchRootId] = useState<string | null>(null);
  const [navNodes, setNavNodes] = useState<TreeNode[] | null>(null);

  function handleBranchSelect(navId: string) {
    // Validate against the complete original tree BEFORE changing state, so
    // an invalid navID leaves the current navigation untouched.
    if (!navNodes || !findNodeById(navNodes, navId)) {
      logger.warn(
        `Branch selected: navID=${navId} was not found in the navigation tree; navigation unchanged.`
      );
      return;
    }

    setBranchRootId(navId);
  }

  function handleBranchError() {
    // Branch Screen unavailable: fall back to the normal iframe content.
    setShowBranches(false);
  }

  function handleNodeClick(node: TreeNode) {
    if (!node.url) return;

    setCurrentUrl(node.url);
    setReloadKey((n) => n + 1);
    setShowBranches(false);

    logUsage(
      usageMonitor,
      buildUsageLogRequest(screenId, node)
    ).catch((err) => {
      logger.error("Failed to log portal usage", err);
    });
  }

  return (
    <div className="portal-screen">
      {config.nav?.left && (
        <aside className="portal-screen-nav portal-screen-nav-left">
          <NavigationPanel
            config={config.nav.left}
            onNodeClick={handleNodeClick}
            rootId={branchRootId}
            onLoaded={setNavNodes}
          />
        </aside>
      )}

      <main className="portal-screen-content">
        {showBranches ? (
          <BranchRenderer
            source={branchesSource}
            onSelect={handleBranchSelect}
            onError={handleBranchError}
          />
        ) : (
          <ContentRenderer
            content={config.content}
            urlOverride={currentUrl}
            reloadKey={reloadKey}
            aiConfig={aiConfig}
          />
        )}
      </main>
    </div>
  );
}
