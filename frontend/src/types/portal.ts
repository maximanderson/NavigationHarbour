export type RendererType = "iframe" | "article" | "download" | "ai" | "html";
export type NavigationType = "tree" | "list" | "tabs" | "buttons";
export type ScreenLayout = "left" | "right" | "top" | "bottom" | "none";
export type TabsPosition = "left" | "center" | "right";

export type ContentConfig =
  | IFrameContentConfig
  | ArticleContentConfig
  | DownloadContentConfig
  | AiContentConfig
  | HtmlContentConfig;

export interface IFrameContentConfig {
  renderer: "iframe";
  defaultUrl: string;
  defaultRenderer?: "branches";
  branchesSource?: string;
  /** Resolved iframe `allow` attribute (Permissions Policy delegation). */
  allow: string;
  /** Resolved iframe `referrerpolicy` attribute. */
  referrerPolicy: IFrameReferrerPolicy;
}

export type IFrameReferrerPolicy =
  | "no-referrer"
  | "no-referrer-when-downgrade"
  | "origin"
  | "origin-when-cross-origin"
  | "same-origin"
  | "strict-origin"
  | "strict-origin-when-cross-origin"
  | "unsafe-url";

export interface BranchRow {
  title: string;
  navId: string;
}

export interface BranchColumn {
  title: string;
  rows: BranchRow[];
}

export interface ArticleContentConfig {
  renderer: "article";
  title: string;
  text: string;
}

export interface DownloadFileConfig {
  label: string;
  url: string;
  fileName?: string;
}

export interface DownloadContentConfig {
  renderer: "download";
  title: string;
  text?: string;
  files: DownloadFileConfig[];
}

export interface AiContentConfig {
  renderer: "ai";
  title: string;
  text?: string;
}

export interface HtmlContentConfig {
  renderer: "html";
  source: string;
}

export interface ApiServiceConfig {
  enabled: boolean;
  protocol: string;
  host: string;
  port: number;
  endpoint: string;
}

export interface UsageMonitorConfig extends ApiServiceConfig {}

export interface AiConfig extends ApiServiceConfig {
  provider: string;
  model: string;
}

export type LogLevel = "debug" | "info" | "warning" | "error";

export interface LoggingConfig {
  enabled: boolean;
  level: LogLevel;
  console: boolean;
}

export interface PortalConfig {
  logging: LoggingConfig;
  portal: {
    title: string;
    defaultScreen: string;
    theme: string;
    tabsPosition: TabsPosition;
  };
  usageMonitor: UsageMonitorConfig;
  ai: AiConfig;
  screens: Record<string, PortalScreenConfig>;
}

export interface PortalScreenConfig {
  title: string;
  layout: ScreenLayout;
  nav?: {
    left?: NavigationPanelConfig;
    right?: NavigationPanelConfig;
    top?: NavigationPanelConfig;
    bottom?: NavigationPanelConfig;
  };
  content: ContentConfig;
}

export interface NavigationPanelConfig {
  title: string;
  type: NavigationType;
  source: string;
  search?: boolean;
  searchMinChars?: number;
  searchAttributes?: string[];
}

export interface TreeNode {
  id: string;
  title: string;
  url?: string;
  renderer?: RendererType;
  children?: TreeNode[];
}
