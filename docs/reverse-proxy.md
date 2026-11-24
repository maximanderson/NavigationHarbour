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
  `X-Forwarded-For` and choose the recorded address. On a shared server, do not run untrusted applications beside Portal
  Core and do not publish Kestrel through a second local forwarder.
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
