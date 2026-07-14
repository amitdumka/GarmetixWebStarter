#!/usr/bin/env python3
"""Create/update Cloudflare Tunnel DNS CNAME and public hostname route.
Works with Cloudflare API token permissions for DNS edit and Zero Trust tunnel edit.
"""
from __future__ import annotations
import argparse
import json
import sys
import urllib.error
import urllib.request

API = "https://api.cloudflare.com/client/v4"


def cf(method: str, path: str, token: str, payload: dict | None = None) -> dict:
    data = None if payload is None else json.dumps(payload).encode()
    req = urllib.request.Request(
        API + path,
        data=data,
        method=method,
        headers={
            "Authorization": f"Bearer {token}",
            "Content-Type": "application/json",
        },
    )
    try:
        with urllib.request.urlopen(req, timeout=30) as resp:
            body = resp.read().decode()
            return json.loads(body)
    except urllib.error.HTTPError as exc:
        body = exc.read().decode(errors="replace")
        raise RuntimeError(f"Cloudflare API {method} {path} failed: HTTP {exc.code}: {body}") from exc


def require_success(result: dict, action: str) -> dict:
    if not result.get("success"):
        raise RuntimeError(f"Cloudflare {action} failed: {json.dumps(result, indent=2)}")
    return result


def update_dns(token: str, zone_id: str, hostname: str, tunnel_id: str) -> None:
    target = f"{tunnel_id}.cfargotunnel.com"
    query = f"/zones/{zone_id}/dns_records?type=CNAME&name={hostname}"
    res = require_success(cf("GET", query, token), "DNS lookup")
    records = res.get("result") or []
    payload = {
        "type": "CNAME",
        "name": hostname,
        "content": target,
        "ttl": 1,
        "proxied": True,
        "comment": "Managed by Garmetix deploy-to-ubuntu.sh",
    }
    if records:
        record_id = records[0]["id"]
        require_success(cf("PUT", f"/zones/{zone_id}/dns_records/{record_id}", token, payload), "DNS update")
        print(f"Updated Cloudflare DNS: {hostname} -> {target}")
    else:
        require_success(cf("POST", f"/zones/{zone_id}/dns_records", token, payload), "DNS create")
        print(f"Created Cloudflare DNS: {hostname} -> {target}")


def update_public_hostname(token: str, account_id: str, tunnel_id: str, hostname: str, service: str) -> None:
    if not account_id:
        print("Skipping public hostname route: account_id not provided", file=sys.stderr)
        return
    base = f"/accounts/{account_id}/cfd_tunnel/{tunnel_id}/configurations"
    config_res = cf("GET", base, token)
    ingress = [{"hostname": hostname, "service": service}, {"service": "http_status:404"}]
    payload = {"config": {"ingress": ingress, "warp-routing": {"enabled": False}}}
    # PUT creates/overwrites the tunnel configuration for named tunnel.
    require_success(cf("PUT", base, token, payload), "tunnel ingress update")
    print(f"Configured tunnel ingress: {hostname} -> {service}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--api-token", required=True)
    parser.add_argument("--zone-id", required=True)
    parser.add_argument("--account-id", default="")
    parser.add_argument("--hostname", required=True)
    parser.add_argument("--tunnel-id", required=True)
    parser.add_argument("--service", default="http://web:3000")
    args = parser.parse_args()
    update_dns(args.api_token, args.zone_id, args.hostname, args.tunnel_id)
    update_public_hostname(args.api_token, args.account_id, args.tunnel_id, args.hostname, args.service)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
