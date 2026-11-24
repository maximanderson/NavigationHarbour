import type {
  PortalConfig,
  PortalScreenConfig,
  ScreenLayout,
  NavigationType,
  RendererType,
  TabsPosition,
  IFrameReferrerPolicy,
  LogLevel,
  LoggingConfig,
} from "../types/portal";
import {
  DEFAULT_LOGGING_CONFIG,
  LOG_LEVELS,
  configureLogger,
  logger,
} from "./logger";
import { resolveApiPort } from "../utils/urlBuilder";

type IniData = Record<string, Record<string, string>>;

interface IFrameOptions {
  allow: string;
  referrerPolicy: IFrameReferrerPolicy;
}

const DEFAULT_IFRAME_OPTIONS: IFrameOptions = {
  allow:
    "accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share",
  referrerPolicy: "strict-origin-when-cross-origin",
};

const REFERRER_POLICIES: readonly IFrameReferrerPolicy[] = [
  "no-referrer",
  "no-referrer-when-downgrade",
  "origin",
  "origin-when-cross-origin",
  "same-origin",
  "strict-origin",
  "strict-origin-when-cross-origin",
  "unsafe-url",
];

export async function loadPortalConfig(
  configUrl = "/config/portal.ini"
): Promise<PortalConfig> {
  const response = await fetch(configUrl);

  if (!response.ok) {
    throw new Error(`Failed to load portal config: ${configUrl}`);
  }

  const iniText = await response.text();
  const ini = parseIni(iniText);

  // Initialise the logger first so that all later diagnostics honour [logging].
  const { config: loggingConfig, issues } = parseLoggingConfig(ini["logging"]);
  configureLogger(loggingConfig);
  for (const issue of issues) {
    logger.warn(issue);
  }

  const portalConfig = buildPortalConfig(ini);
  portalConfig.logging = loggingConfig;

  return portalConfig;
}

function parseLoggingConfig(section: Record<string, string> | undefined): {
  config: LoggingConfig;
  issues: string[];
} {
  const config: LoggingConfig = { ...DEFAULT_LOGGING_CONFIG };
  const issues: string[] = [];

  const parseBool = (key: "enabled" | "console") => {
    const raw = section?.[key];
    if (raw === undefined) return;

    const value = raw.trim().toLowerCase();
    if (value === "true") config[key] = true;
    else if (value === "false") config[key] = false;
    else {
      issues.push(
        `[logging] invalid ${key} "${raw}"; using "${DEFAULT_LOGGING_CONFIG[key]}".`
      );
    }
  };

  parseBool("enabled");
  parseBool("console");

  const rawLevel = section?.level;
  if (rawLevel !== undefined) {
    const level = rawLevel.trim().toLowerCase() as LogLevel;
    if (LOG_LEVELS.includes(level)) {
      config.level = level;
    } else {
      issues.push(
        `[logging] invalid level "${rawLevel}"; using "${DEFAULT_LOGGING_CONFIG.level}".`
      );
    }
  }

  return { config, issues };
}

function parseIni(text: string): IniData {
  const result: IniData = {};
  let currentSection = "";

  for (const rawLine of text.split(/\r?\n/)) {
    const line = rawLine.trim();

    if (!line || line.startsWith(";") || line.startsWith("#")) {
      continue;
    }

    const sectionMatch = line.match(/^\[(.+)]$/);
    if (sectionMatch) {
      currentSection = sectionMatch[1].trim();
      result[currentSection] = {};
      continue;
    }

    const equalsIndex = line.indexOf("=");
    if (equalsIndex === -1 || !currentSection) {
      continue;
    }

    const key = line.slice(0, equalsIndex).trim();
    const value = line.slice(equalsIndex + 1).trim();

    result[currentSection][key] = value;
  }

  return result;
}

function resolveIFrameOptions(
  section: Record<string, string> | undefined,
  inherited: IFrameOptions,
  context: string
): IFrameOptions {
  // An explicitly present `allow` (even empty) overrides the inherited value.
  const allow = section?.allow ?? inherited.allow;
  let referrerPolicy = inherited.referrerPolicy;

  if (section?.referrerPolicy !== undefined) {
    const value = section.referrerPolicy;

    if (REFERRER_POLICIES.includes(value as IFrameReferrerPolicy)) {
      referrerPolicy = value as IFrameReferrerPolicy;
    } else {
      logger.warn(
        `${context}: invalid referrerPolicy "${value}"; using "${inherited.referrerPolicy}".`
      );
    }
  }

  return { allow, referrerPolicy };
}

function buildPortalConfig(ini: IniData): PortalConfig {
  const portalSection = ini["portal"];

  if (!portalSection) {
    throw new Error("Missing [portal] section in portal.ini");
  }

  const usageSection = ini["usage_monitor"];
  const aiSection = ini["ai"];
  const iframeDefaults = resolveIFrameOptions(
    ini["renderer.iframe"],
    DEFAULT_IFRAME_OPTIONS,
    "[renderer.iframe]"
  );

  const config: PortalConfig = {
    logging: { ...DEFAULT_LOGGING_CONFIG },
    portal: {
      title: portalSection.title ?? __PRODUCT_NAME__,
      defaultScreen: portalSection.defaultScreen ?? "",
      theme: portalSection.theme ?? "light",
      tabsPosition: (portalSection.tabsPosition ?? "right") as TabsPosition,
    },
    usageMonitor: {
      enabled: usageSection?.enabled === "true",
      protocol: usageSection?.protocol ?? "http",
      host: usageSection?.host ?? "",
      port: resolveApiPort(usageSection?.host, usageSection?.port),
      endpoint: usageSection?.endpoint ?? "/api/usage",
    },
    ai: {
      enabled: aiSection?.enabled === "true",
      provider: aiSection?.provider ?? "local",
      protocol: aiSection?.protocol ?? "http",
      host: aiSection?.host ?? "",
      port: resolveApiPort(aiSection?.host, aiSection?.port),
      endpoint: aiSection?.endpoint ?? "/api/ai/query",
      model: aiSection?.model ?? "placeholder",
    },
    screens: {},
  };

  for (const sectionName of Object.keys(ini)) {
    const screenMatch = sectionName.match(/^screen\.([^.]+)$/);

    if (screenMatch) {
      const screenId = screenMatch[1];
      const section = ini[sectionName];

      config.screens[screenId] = {
        title: section.title ?? screenId,
        layout: (section.layout ?? "left") as ScreenLayout,
        content: {
          renderer: "iframe",
          defaultUrl: "",
          ...iframeDefaults,
        },
      };
    }
  }

  for (const sectionName of Object.keys(ini)) {
    const navMatch = sectionName.match(
      /^screen\.([^.]+)\.nav\.(left|right|top|bottom)$/
    );

    if (navMatch) {
      const screenId = navMatch[1];
      const position = navMatch[2] as
        | "left"
        | "right"
        | "top"
        | "bottom";
      const section = ini[sectionName];

      const screen = getOrCreateScreen(config, screenId, iframeDefaults);

      screen.nav ??= {};
      screen.nav[position] = {
        title: section.title ?? "Navigation",
        type: (section.type ?? "tree") as NavigationType,
        source: section.source ?? "",
        search: section.search === "true",
        searchMinChars: Number(section.searchMinChars ?? 3),
        searchAttributes: (section.searchAttributes ?? "title")
          .split(",")
          .map((s) => s.trim())
          .filter(Boolean),
      };
    }

    const contentMatch = sectionName.match(/^screen\.([^.]+)\.content$/);

    if (contentMatch) {
      const screenId = contentMatch[1];
      const section = ini[sectionName];

      const screen = getOrCreateScreen(config, screenId, iframeDefaults);

      const renderer =
        (section.renderer ?? "iframe") as RendererType;

      if (renderer === "iframe") {
        screen.content = {
          renderer,
          defaultUrl: section.defaultUrl ?? "",
          ...resolveIFrameOptions(
            section,
            iframeDefaults,
            `Screen "${screenId}"`
          ),
        };

        if (section.defaultRenderer === "branches") {
          if (section.branchesSource) {
            screen.content.defaultRenderer = "branches";
            screen.content.branchesSource = section.branchesSource;
          } else {
            logger.warn(
              `Screen "${screenId}": defaultRenderer = branches requires branchesSource.`
            );
          }
        }
      }

      if (renderer === "html") {
        screen.content = {
          renderer,
          source: section.source ?? "",
        };
      }

      if (renderer === "ai") {
        screen.content = {
          renderer,
          title: section.title ?? "AI Assistant",
          text: section.text ?? "",
        };
      }
    }
  }

  return config;
}

function getOrCreateScreen(
  config: PortalConfig,
  screenId: string,
  iframeDefaults: IFrameOptions
): PortalScreenConfig {
  if (!config.screens[screenId]) {
    config.screens[screenId] = {
      title: screenId,
      layout: "left",
      content: {
        renderer: "iframe",
        defaultUrl: "",
        ...iframeDefaults,
      },
    };
  }

  return config.screens[screenId];
}
