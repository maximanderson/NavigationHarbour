# Reverse Proxy Deployment (client IP address)

When Navigation Harbour runs behind a reverse proxy (IIS with Application Request Routing, Nginx, Apache, a load balancer, ...),
every request reaches Kestrel from the proxy. Without further configuration the usage database would record the
proxy's address (typically `127.0.0.1`) for every user.

Navigation Harbour uses the standard ASP.NET Core **Forwarded Headers Middleware** to recover the real client address from the
`X-Forwarded-For` header. It works with any proxy that sets that header. No frontend change, database change or extra
package is needed, and the usage logging code is unchanged: it reads the connection address, which the middleware
corrects.

## Direct deployment versus reverse proxy deployment

| | Direct | Behind a reverse proxy |
|---|---|---|
| Browsers connect to | Navigation Harbour (Kestrel) | The proxy, which forwards to Navigation Harbour |
| Address Kestrel sees | The real client | The proxy |
| `[proxy]` settings | Not needed | Not needed for a proxy on the same computer; required for a proxy on another computer |
| `X-Forwarded-For` | Ignored unless it comes from a trusted proxy | Used when it comes from a trusted proxy |

A direct deployment behaves exactly as before: a request from any non-trusted address is never affected by
`X-Forwarded-For`, so clients cannot fake their address.

## How Navigation Harbour decides

1. The peer that opened the TCP connection to Kestrel is checked against the **trusted proxies**.
2. If it is trusted, the **last** address in `X-Forwarded-For` becomes the client address.
3. If it is not trusted, or there is no header, the peer address is kept and the header is ignored.

Trusted by default (framework default, always kept): loopback, i.e. `127.0.0.0/8` and `::1`.
Everything in `[proxy]` is **added** to that. Nothing can switch the trust check off.

## The `[proxy]` section of `server.ini`

The section is optional. These settings belong in the private `conf/server.ini`, **not** in `portal.ini`, which is
served to browsers.

```ini
[proxy]
trusted_proxies =
trusted_networks =
```

| Setting | Default | Meaning |
|---|---|---|
| `trusted_proxies` | *(empty)* | Comma-separated IPv4/IPv6 addresses of proxies, e.g. `192.0.2.5, fd00::5` |
| `trusted_networks` | *(empty)* | Comma-separated CIDR networks, e.g. `192.0.2.0/24, fd00:1234::/64` |

- Empty or absent: only loopback is trusted. Existing `server.ini` files without `[proxy]` keep working unchanged.
- Entries are validated at startup and by `--check-config`. A malformed address, a host name, a prefix out of range, a
  network with host bits set (`192.0.2.5/24`), an empty list item, or `0.0.0.0/0` / `::/0` (everyone) is an error.
- Only `X-Forwarded-For` is processed. `X-Forwarded-Proto` and `X-Forwarded-Host` are ignored.
- Changes need a restart.
- Like other settings, they can also be supplied by environment variable, for example
  `PORTAL_proxy__trusted_proxies=192.0.2.5`.

`--print-config` lists both settings as `[applied]`.

## Same-host reverse proxy

The proxy and Navigation Harbour run on the same computer and the proxy connects to `127.0.0.1` / `localhost`.
**No `[proxy]` configuration is needed.** Keep Kestrel on loopback:

```ini
[server]
protocol = http
http_addr = 127.0.0.1
port = 5247
```

## Remote reverse proxy

*Not manually validated; covered by automated tests only.*

The proxy runs on another computer (or reaches Navigation Harbour from a non-loopback address). Add it explicitly:

```ini
[server]
protocol = http
; the address Navigation Harbour listens on, reachable only from the proxy
http_addr = 192.0.2.20
port = 5247

[proxy]
trusted_proxies = 192.0.2.5
; or a range:
; trusted_networks = 192.0.2.0/28
```

Also restrict access with a firewall so that only the proxy can connect to that port.

## Proxy configuration examples

> **Validation status.**
> **Automated tests:** the Navigation Harbour side is covered by `Portal.Api.Tests` (`ForwardedHeadersTests`,
> `ProxyConfigTests`): configuration parsing, trust decisions for trusted and untrusted peers, IPv4 and IPv6, single
> hop, and a real Kestrel host receiving a forwarded request over loopback.
> **Manually validated:** a **same-host IIS ARR** deployment on Windows (ARR and Navigation Harbour on the same computer,
> `[proxy]` left empty). The original client addresses were correctly recorded in the usage database.
> **Not manually validated:** the Nginx and Apache snippets below, and any **remote** proxy deployment (a proxy on
> another computer listed in `[proxy]`). They are standard settings written from the products' documentation. Treat
> them as **unvalidated examples** and verify them in your own environment with the procedure under "Verification".
> Host names and addresses are placeholders.

### IIS with Application Request Routing (ARR)

*Manually validated for the same-host case (v1.0.1).*

1. Install URL Rewrite and ARR, and create a rewrite rule that forwards to `http://127.0.0.1:5247/{R:1}`.
2. In IIS Manager, select the server node, open **Application Request Routing Cache**, then **Server Proxy Settings**,
   and enable **Preserve client IP in the following HTTP header** with the value `X-Forwarded-For`.
3. Leave `[proxy]` empty when ARR and Navigation Harbour are on the same computer.

### Sharing an existing HTTPS website with other web applications (IIS, ARR, URL Rewrite)

*Manually validated (same-host IIS ARR). All host names, paths and ports below are generic placeholders.*

Navigation Harbour does **not** need a website of its own. If an HTTPS website already exists in IIS and hosts other web
applications, an existing reverse proxy can route requests to the local Kestrel endpoint:

- an existing application stays reachable under its original URL path, for example `https://apps.example.com/ExistingApp/`;
- Navigation Harbour is reachable from the root URL, `https://apps.example.com/`;
- the rewrite rule **excludes** the existing application's path and forwards every other request to
  `http://127.0.0.1:5247/`.

Navigation Harbour must be served at the root of a host name; sub-path hosting is not supported (see
[standalone-hosting.md](standalone-hosting.md)). This is why Navigation Harbour takes the root and the existing application keeps its sub-path.

**Prerequisites:** URL Rewrite and ARR are installed, proxying is enabled in ARR (**Server Proxy Settings > Enable proxy**,
with **Preserve client IP** set to `X-Forwarded-For` as above), and `server.ini` binds Kestrel to loopback
(`protocol = http`, `http_addr = 127.0.0.1`, `port = 5247`). Leave `[proxy]` empty for a same-host proxy.

**Rule.** In the existing website's `web.config` (or in IIS Manager > URL Rewrite), add the rule **after** any rules that
belong to other applications:

```xml
<system.webServer>
  <rewrite>
    <rules>
      <rule name="NavigationHarbour" stopProcessing="true">
        <!-- Match everything except /ExistingApp and anything beneath it -->
        <match url="^(?!ExistingApp(?:/|$))(.*)$" />
        <conditions logicalGrouping="MatchAny">
          <!-- Only the intended host names (generic examples) -->
          <add input="{HTTP_HOST}" pattern="^harbour\.example\.com$" />
          <add input="{HTTP_HOST}" pattern="^apps\.example\.com$" />
        </conditions>
        <action type="Rewrite" url="http://127.0.0.1:5247/{R:1}" />
      </rule>
    </rules>
  </rewrite>
</system.webServer>
```

How it works:

- `^(?!ExistingApp(?:/|$))(.*)$` matches every path **except** `ExistingApp` itself and `ExistingApp/...`. Because the
  match is anchored, a path such as `ExistingApplication` or `ExistingApp2` is *not* excluded and is forwarded to
  Navigation Harbour. IIS URL Rewrite matches against the path without the leading slash, and the match is
  case-insensitive by default.
- Excluded requests are not rewritten, so IIS handles them with the existing application.
  To protect several applications, list them in one group: `^(?!(?:ExistingApp|OtherApp)(?:/|$))(.*)$`.
- The host conditions restrict the rule to the intended host names. Requests for other host names on the same site
  are not forwarded. `{R:1}` carries the path; the query string is forwarded automatically.
- The existing applications keep their **own** authentication and authorisation settings (for example Windows
  authentication on their IIS application). Navigation Harbour does not change, replace or inherit them.
  Navigation Harbour itself has no login (see the README security section).

**DNS, certificate and bindings**

- Create the DNS alias (a CNAME, or an A record) for each host name that should open Navigation Harbour, pointing at the
  server that hosts the website.
- The website's HTTPS binding must accept each host name (add a binding per host name, or use a wildcard or
  multi-name (SAN) certificate that covers them). The certificate must be trusted by the users' browsers and
  must match every host name used.
- If the website has several bindings, make sure each host name used in the conditions has a binding on the site that
  holds the rule.
- Only the website's HTTPS port is exposed to users. Kestrel stays on `127.0.0.1` and is not published.

**Same-origin iframe considerations**

- Applications opened from Navigation Harbour in an iframe, and hosted on the **same host name**, are same-origin with it.
  Cookies and sign-in then behave as for a normal page, which often avoids third-party cookie problems.
- Applications on another host name are cross-site. Their sign-in cookies depend on their own `SameSite` settings.
- Whether a page may be embedded is decided **by the embedded application** through its own security headers
  (`X-Frame-Options`, `Content-Security-Policy: frame-ancestors`) and cookie settings. Navigation Harbour does not
  override them. Do not remove or weaken those controls to make a frame appear. If a page must be embeddable, allow
  the Navigation Harbour origin explicitly in that application's own configuration, for example
  `frame-ancestors 'self' https://harbour.example.com`, as its owner decides.
- Use `https://` URLs for embedded pages when Navigation Harbour is served over HTTPS (mixed content is blocked).

**Check it**

1. `https://apps.example.com/` shows Navigation Harbour; `https://apps.example.com/ExistingApp/` still shows the existing application.
2. Open a screen and click a leaf; the usage database records the workstation's address, not `127.0.0.1`
   (see "Verification").
3. If the root shows the existing site instead, check the rule order and the host conditions; if you get `502` or
   `500`, check that proxying is enabled in ARR and that Navigation Harbour is running on the local port.

### Nginx

```nginx
server {
    listen 443 ssl;
    server_name portal.example.com;

    location / {
        proxy_pass http://127.0.0.1:5247;
        proxy_set_header Host $host;
        # Overwrite; never forward a client-supplied value:
        proxy_set_header X-Forwarded-For $remote_addr;
    }
}
```

### Apache (mod_proxy, mod_proxy_http)

```apache
<VirtualHost *:443>
    ServerName portal.example.com
    ProxyPreserveHost On
    ProxyPass        / http://127.0.0.1:5247/
    ProxyPassReverse / http://127.0.0.1:5247/
    # mod_proxy adds X-Forwarded-For by default (ProxyAddHeaders On).
</VirtualHost>
```

For a **remote** proxy, replace `127.0.0.1` with Navigation Harbour's address and list the proxy in `[proxy]`.

## Security considerations

- **Trust only proxies you control.** Every address in `trusted_proxies` / `trusted_networks` can state any client
  address. Use the narrowest list possible; prefer single addresses over networks.
- **Loopback is always trusted.** Any process on the Navigation Harbour computer, and anyone who can reach Kestrel over
  loopback (for example another web application on the same host that forwards user-controlled requests), can set
  `X-Forwarded-For` and choose the recorded address. On a shared server, do not run untrusted applications beside Navigation
  Harbour and do not publish Kestrel through a second local forwarder.
- **Do not expose Kestrel to untrusted networks when it is meant to sit behind a proxy.** Bind it to `127.0.0.1` or a
  private address and use a firewall. Direct clients cannot fake their address (their header is ignored), but they
  bypass the proxy's other protections.
- **The proxy should overwrite or append `X-Forwarded-For`.** Because only the last entry is used, a value supplied by the
  client in front of it is ignored, provided the proxy appends its own entry.
- **The recorded address is personal data.** Handle the usage database accordingly.
- **Trust checking cannot be disabled.** There is no "trust everyone" setting and `0.0.0.0/0` is refused.
  Do not use the framework's `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` variable: it trusts every sender.

## Single-hop limitation

Navigation Harbour accepts exactly **one** proxy hop (`ForwardLimit = 1`): the address immediately before the trusted peer.
With a chain (for example a load balancer in front of an IIS or Nginx server in front of Navigation Harbour) the recorded
address is the previous proxy's, not the browser's. Multi-hop forwarding is not supported. If you need the original
client address through a chain, let the proxy nearest to Navigation Harbour **overwrite** `X-Forwarded-For` with the client
address it has determined (for example Nginx `real_ip`).

## Verification

1. `NavigationHarbour --check-config` reports `Result: OK`, and `--print-config` shows `[proxy]` as applied.
2. Open the portal from a workstation through the proxy and open a screen.
3. Look at the latest usage rows:
   `sqlite3 data/usage.db "select ClientIp, TimestampUtc from UsageLogs order by Id desc limit 5;"`
   The workstation's address must appear, not `127.0.0.1`.
4. Same-host test of the trust rule (Navigation Harbour on port 5247):
   `curl -X POST http://127.0.0.1:5247/api/usage -H "Content-Type: application/json" -H "X-Forwarded-For: 203.0.113.9" -d "{}"`
   records `203.0.113.9`, because loopback is trusted.
5. From another computer, call Kestrel directly with a fake header (needs a non-loopback `http_addr`):
   `curl -X POST http://<server>:5247/api/usage -H "Content-Type: application/json" -H "X-Forwarded-For: 198.51.100.77" -d "{}"`
   must record the caller's real address, not `198.51.100.77`.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| Still `127.0.0.1` | The proxy does not send `X-Forwarded-For` (check the ARR "Preserve client IP" setting or the `proxy_set_header` line), or requests bypass the proxy |
| The proxy's address is recorded (remote proxy) | The proxy is not in `trusted_proxies` / `trusted_networks`, or it connects from a different address than the one listed |
| An inner proxy's address is recorded | More than one hop: see "Single-hop limitation" |
| Startup error `[proxy] ...` | Invalid value; the message names the entry. Run `--check-config` |
| IPv6 form such as `::ffff:192.0.2.9` | An IPv4 client seen through a dual-stack listener; it is the same client |

To see what reaches the backend, send a test request with `curl` as above, or check the proxy's own access log.

See also: [server-configuration.md](server-configuration.md), [windows-deployment.md](windows-deployment.md),
[standalone-hosting.md](standalone-hosting.md).
