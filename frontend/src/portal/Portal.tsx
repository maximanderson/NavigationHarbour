//portal/Portal.tsx
import { useState } from "react";
import type { PortalConfig } from "../types/portal";
import { PortalScreen } from "../screens/PortalScreen";

interface PortalProps {
  config: PortalConfig;
}

export function Portal({ config }: PortalProps) {
  const [activeScreenId, setActiveScreenId] = useState(
    config.portal.defaultScreen
  );

  // Incremented when the already-active tab is clicked again. It is part of
  // the PortalScreen key, so the screen remounts and returns to its
  // configured initial state.
  const [resetCounter, setResetCounter] = useState(0);

  function handleTabClick(screenId: string) {
    if (screenId === activeScreenId) {
      setResetCounter((n) => n + 1);
      return;
    }

    setActiveScreenId(screenId);
  }

  const activeScreen =
    config.screens[activeScreenId] ??
    config.screens[Object.keys(config.screens)[0]];

  return (
    <div className={`portal portal-theme-${config.portal.theme}`}>
      {/* HEADER */}
      <header className="portal-header">
        <div className="portal-title">{config.portal.title}</div>

        <nav
          className={`portal-tabs portal-tabs-${config.portal.tabsPosition}`}
        >
          {Object.entries(config.screens).map(([screenId, screen]) => (
            <button
              key={screenId}
              className={`portal-tab ${
                screenId === activeScreenId ? "active" : ""
              }`}
              onClick={() => handleTabClick(screenId)}
            >
              {screen.title}
            </button>
          ))}
        </nav>
      </header>

      {/* BODY */}
      <div className="portal-body">
        <PortalScreen
          key={`${activeScreenId}:${resetCounter}`}
          screenId={activeScreenId}
          config={activeScreen}
          usageMonitor={config.usageMonitor}
          aiConfig={config.ai}
        />
      </div>
    </div>
  );
}
