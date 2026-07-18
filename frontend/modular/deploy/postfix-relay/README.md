# Optional Postfix-to-Brevo Relay (CM-10)

**This is optional, isolated infrastructure. It is not deployed, started, or referenced by
any script in this repository. Most installations never need it** - the Communication & Mail
module's SMTP and Brevo API providers (configured under Providers in the app) already talk to
Brevo directly with no relay in between. Only stand this up if you specifically want a local
SMTP hop (e.g. to centralize outbound mail from multiple internal services through one
egress point).

## What this is

A single `docker compose` service running Postfix in relay-only mode:
internal senders (restricted to the CIDR ranges you configure) → this container → Brevo's
SMTP relay over TLS → the internet. It never accepts mail from the public internet and never
delivers mail directly - it only forwards, authenticated, through Brevo.

## Setup

1. `cd frontend/modular/deploy/postfix-relay`
2. `cp .env.example .env` and fill in:
   - `BREVO_SMTP_USERNAME` / `BREVO_SMTP_PASSWORD` - the same SMTP credentials you would enter
     for an SMTP > Brevo preset Provider in Communication & Mail > Providers.
   - `RELAY_ALLOWED_NETWORKS` - your application's own internal network CIDR only. Never
     `0.0.0.0/0`.
   - `RELAY_MAIL_DOMAIN` - the domain this relay identifies as.
   - `RELAY_BIND_ADDRESS` - leave as `127.0.0.1` unless you have a specific, firewalled reason
     to bind more broadly.
3. `docker compose up -d`
4. Verify: `./healthcheck.sh` (also usable directly from cron/monitoring - it never touches
   the main application).

## Operational notes

- **Logs**: captured via the `json-file` Docker logging driver with `max-size: 10m` /
  `max-file: 5` (automatic rotation, ~50MB cap per container) in `docker-compose.yml`, plus
  Postfix's own `maillog_file` inside the container (`postfix/main.cf.template`). Inspect with
  `docker logs garmetix-communication-postfix-relay` or `docker exec ... tail -f /var/log/postfix.log`.
- **Queue inspection**: `docker exec garmetix-communication-postfix-relay postqueue -p` lists
  anything stuck; `postqueue -f` forces a retry flush.
- **Sender restriction**: `postfix/main.cf.template`'s `smtpd_sender_restrictions` requires a
  `sender_access` allow-list (default-deny) - populate
  `/etc/postfix/sender_access` inside the container with approved FROM addresses/domains
  mapped to `OK`, then `postmap /etc/postfix/sender_access` to regenerate the lookup table.
- **Credential protection**: `BREVO_SMTP_PASSWORD` lives only in your local, gitignored `.env`
  file (matches this repo's existing root `.gitignore` `.env`/`.env.*`/`!.env.example`
  pattern) - never commit it, never put it in `docker-compose.yml` directly.
- **Disabling**: `docker compose down` in this directory. Nothing elsewhere in the repository
  depends on this relay existing or running.

## DNS / deliverability setup (do this regardless of whether you use this relay)

These apply to the Brevo sending domain itself, not to this relay container - required
whether you send via Brevo API/SMTP directly or through this relay:

- **SPF**: one TXT record per domain; Brevo will tell you the exact `include:` value to add.
  Do not create a second, conflicting SPF TXT record - merge into your existing one if you
  already have one for other senders.
- **DKIM**: Brevo generates a CNAME/TXT pair per sending domain from its dashboard - add both
  exactly as shown.
- **DMARC**: start with `p=none; rua=mailto:you@yourdomain.com` to observe before enforcing;
  move to `p=quarantine` then `p=reject` once you've confirmed legitimate mail passes.
- **Reply-To**: if recipients need to reply to a real mailbox (not a no-reply address),
  configure that mailbox's address as the Reply-To Email field on the Provider in
  Communication & Mail > Providers - this is independent of the relay/DNS setup above.

## Never do this

- Never expose port 25 (or the container's mapped port) to `0.0.0.0` on a public-facing host.
- Never widen `RELAY_ALLOWED_NETWORKS` beyond your own application's network.
- Never wire this compose file into `srp-whole-site-deploy.sh` or any other automatic deploy
  path - it must always be a deliberate, separate `docker compose up` an operator runs by hand.
