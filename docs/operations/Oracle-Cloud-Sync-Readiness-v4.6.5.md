# Oracle Cloud Sync Readiness v4.6.5

Run after deployment:

```bash
cd /opt/garmetix/current
./scripts/linux/oracle-cloud-readiness-check.sh .env.production
```

## Required environment values

```env
ORACLE_SYNC_ENABLED=false
ORACLE_SYNC_DIRECTION=Bidirectional
ORACLE_SYNC_CONFLICT_POLICY=ManualReview
ORACLE_SYNC_TNS_ADMIN=/opt/garmetix/oracle-wallet
ORACLE_SYNC_WALLET_DIRECTORY=/opt/garmetix/oracle-wallet
ORACLE_SYNC_CONNECTION_STRING=your_tns_alias_or_connection_string
ORACLE_SYNC_TRUSTED_SOURCE_APPLICATIONS=ExternalTailoringApp,ExternalInventoryApp
ORACLE_SYNC_REQUIRE_TRUSTED_SOURCE_FOR_AUTO_APPLY=true
ORACLE_SYNC_APPLY_INBOUND_AUTOMATICALLY=false
```

Keep auto-apply disabled until live external-app events have passed manual review. Start with manual review, then enable auto-apply only for trusted sources and low-risk entities.

## Go-live acceptance

- Wallet/TNS directory exists and is readable by the API container.
- `/api/oracle-sync/status` reports the intended direction and conflict policy.
- `/api/oracle-sync/cloud-readiness` reports no critical configuration gaps.
- `/api/oracle-sync/external-app-test-plan` lists the planned test cases.
- A live `/api/oracle-sync/test` succeeds after credentials are configured.
- Inbound events can be pulled, reviewed, applied, rejected, retried, and dead-lettered safely.
