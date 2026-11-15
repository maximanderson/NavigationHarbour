import { useEffect, useState } from "react";
import { Portal } from "../portal/Portal";
import { loadPortalConfig } from "../services/configService";
import { logger } from "../services/logger";
import type { PortalConfig } from "../types/portal";
import "./App.css";

function App() {
  const [config, setConfig] = useState<PortalConfig | null>(null);
  const [error, setError] = useState<string>("");

  useEffect(() => {
    loadPortalConfig()
      .then(setConfig)
      .catch((err) => {
        // The UI message is always shown, independent of logger settings
        // (the logger may not be configured if portal.ini failed to load).
        logger.error("Unable to load portal configuration:", err);
        setError(err instanceof Error ? err.message : String(err));
      });
  }, []);

  if (error) {
    return (
      <div className="app-error">
        Unable to load portal configuration.
        {error && <div>{error}</div>}
      </div>
    );
  }

  if (!config) {
    return <div className="app-loading">Loading portal…</div>;
  }

  return <Portal config={config} />;
}

export default App;