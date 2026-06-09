#!/usr/bin/env python3
from pathlib import Path
import sys

root = Path(__file__).resolve().parents[2]
checks = [
    ("backend/Garmetix.Api/Production/ProductionReadinessEndpoints.cs", "MapProductionReadinessEndpoints"),
    ("backend/Garmetix.Api/Production/ProductionReadinessDtos.cs", "ProductionReadinessSummaryDto"),
    ("backend/Garmetix.Api/Program.cs", "app.MapProductionReadinessEndpoints();"),
    ("backend/Garmetix.Api/Program.cs", "Cors:AllowedOriginsCsv"),
    ("backend/Garmetix.Api/Program.cs", "X-Content-Type-Options"),
    ("frontend/garmetix-web/pages/production-readiness/index.vue", "production-readiness/summary"),
    ("frontend/garmetix-web/components/AppShell.vue", "/production-readiness"),
    ("docker-compose.prod.yml", "ASPNETCORE_FORWARDEDHEADERS_ENABLED"),
    ("docker-compose.prod.yml", "Cors__AllowedOriginsCsv"),
    ("docker-compose.prod.yml", "max-size"),
    (".env.production.example", "CORS_ALLOWED_ORIGINS"),
    ("scripts/linux/generate-secrets.sh", "JWT_SIGNING_KEY"),
    ("scripts/linux/production-preflight.sh", "docker compose"),
    ("scripts/linux/deploy-release.sh", "rollback"),
    ("scripts/linux/rollback-release.sh", "previous"),
    ("scripts/linux/monitor-health.sh", "/api/health"),
    ("scripts/linux/start-cloudflare-tunnel.sh", "cloudflared"),
    ("infra/caddy/Caddyfile.example", "reverse_proxy"),
    ("infra/cloudflare/config.example.yml", "ingress"),
    ("deploy/systemd/garmetix.service", "docker compose"),
    ("Production-Environment-Hardening.md", "Stage 5B"),
]

errors = []
for rel, needle in checks:
    path = root / rel
    if not path.exists():
        errors.append(f"missing {rel}")
        continue
    text = path.read_text(errors='ignore')
    if needle not in text:
        errors.append(f"{rel} missing {needle!r}")

# Light C# brace balance check for new endpoint files.
for rel in ["backend/Garmetix.Api/Production/ProductionReadinessEndpoints.cs", "backend/Garmetix.Api/Production/ProductionReadinessDtos.cs"]:
    text = (root / rel).read_text(errors='ignore')
    if text.count('{') != text.count('}'):
        errors.append(f"brace mismatch in {rel}")

# Vue template sanity.
vue = root / "frontend/garmetix-web/pages/production-readiness/index.vue"
text = vue.read_text(errors='ignore')
if '<template>' not in text or '</template>' not in text:
    errors.append("production-readiness Vue template tag mismatch")
if text.count('<script setup') != 1 or text.count('</script>') != 1:
    errors.append("production-readiness Vue script tag mismatch")

if errors:
    print("Stage 5B static validation failed:")
    for error in errors:
        print(f"- {error}")
    sys.exit(1)

print("Stage 5B static validation passed.")
