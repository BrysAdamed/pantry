# 0006 No public exposure; HTTPS on a private hostname

**Status:** Accepted. The TLS option is decided in epic E10.

## Context
The app runs in a homelab on Docker. We don't want a public IP or open ports. Phones sync when they are on the home network and work offline otherwise (see 0007). Passkeys, service workers and iOS PWA install all need HTTPS on a real hostname; a bare IP or self-signed certificate is not enough.

## Decision
- The app is reachable on the home network only.
- The app itself speaks plain HTTP inside Docker; a reverse proxy terminates TLS.
- The hostname is configuration, never hardcoded.
- TLS options, to be picked in E10:
  1. **Recommended:** own domain, wildcard Let's Encrypt certificate through the DNS-01 challenge (e.g. Caddy with a DNS provider API), wildcard DNS record pointing to the LAN IP. One certificate covers every homelab app; no port is opened.
  2. Tailscale with `*.ts.net` certificates. Also gives access away from home, needs the Tailscale app on phones.
  3. Own certificate authority installed on devices. Not recommended (painful on iOS).

## Consequences
- Development is unaffected: `localhost` is a secure context.
- Phone use (install, passkeys) waits for the E10 decision.
- Some routers block public DNS answers that point to private IPs (DNS-rebind protection); the domain may need whitelisting.
- A wildcard certificate keeps individual subdomain names out of public Certificate Transparency logs.
