// Plain Node test (no test framework needed): node --test --experimental-strip-types src/utils/urlBuilder.test.mjs
// Run with:  npm run test:urls
import assert from "node:assert/strict";
import { test } from "node:test";
import { buildApiUrl, resolveApiPort } from "./urlBuilder.ts";

const api = (overrides) => ({ protocol: "http", host: "", port: 0, endpoint: "/api/usage", ...overrides });

test("an empty host gives a same-origin relative URL (standalone hosting)", () => {
  assert.equal(buildApiUrl(api({})), "/api/usage");
  assert.equal(buildApiUrl(api({ endpoint: "/api/ai/query" })), "/api/ai/query");
});

test("an explicit host keeps the absolute URL (Vite development, external API server)", () => {
  assert.equal(buildApiUrl(api({ host: "localhost", port: 5247 })), "http://localhost:5247/api/usage");
  assert.equal(
    buildApiUrl(api({ protocol: "https", host: "api.example.com", port: 8443, endpoint: "/api/ai/query" })),
    "https://api.example.com:8443/api/ai/query"
  );
});

test("an explicit host with port 0 uses the protocol default port", () => {
  assert.equal(buildApiUrl(api({ protocol: "https", host: "api.example.com", port: 0 })), "https://api.example.com/api/usage");
});

test("the port is ignored when there is no host", () => {
  assert.equal(buildApiUrl(api({ port: 5247 })), "/api/usage");
});

test("resolveApiPort: no host means no port, a host without a port keeps the old default 5247", () => {
  assert.equal(resolveApiPort(undefined, undefined), 0);
  assert.equal(resolveApiPort("", "5247"), 0);
  assert.equal(resolveApiPort("localhost", undefined), 5247);
  assert.equal(resolveApiPort("localhost", "6000"), 6000);
  assert.equal(resolveApiPort("api.example.com", "443"), 443);
});
