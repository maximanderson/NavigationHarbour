interface ApiServiceConfig {
  protocol: string;
  host: string;
  port: number;
  endpoint: string;
}

/**
 * Builds the URL of a backend API call.
 * - host empty (or omitted in portal.ini): same-origin relative URL, e.g. "/api/usage". This is what a
 *   standalone installation uses, where one server serves both the portal and the API.
 * - host set: absolute URL to that server (development with Vite, or an external API server).
 *   A port of 0 means "no explicit port" (the protocol default).
 */
export function buildApiUrl(config: ApiServiceConfig): string {
  if (!config.host) {
    return config.endpoint;
  }

  const port = config.port > 0 ? `:${config.port}` : "";
  return `${config.protocol}://${config.host}${port}${config.endpoint}`;
}

/**
 * Port of an API service from portal.ini. With no host the calls are same-origin, so the port is unused (0).
 * With a host, a missing port keeps the previous default of 5247 (existing development setup).
 */
export function resolveApiPort(host: string | undefined, port: string | undefined): number {
  if (!host) {
    return 0;
  }

  return Number(port ?? 5247);
}
