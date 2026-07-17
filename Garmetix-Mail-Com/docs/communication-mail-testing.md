# Test and Acceptance Plan

## Automated tests

Cover template escaping/variables, secret masking/rotation, provider error classification, state transitions, retry timing, idempotency, recipient resolution, tenant filtering, suppression, webhook mapping/deduplication, attachment authorization and SMTP presets.

Integration-test queue creation, atomic multi-worker claims, stale lease recovery, transient retry, permanent failure, dead-letter recovery, provider CRUD, blank-secret retention, internal read state, cross-tenant rejection and webhook delivery updates.

Security-test forged webhooks, header injection, unsafe HTML, oversized/dangerous attachments, traversal, ID enumeration, secret leakage in JSON/logs and unauthorized provider/queue actions.

## Manual acceptance

1. Create a Brevo provider and verify the saved key cannot be read back.
2. Test connection and queue a test email.
3. Confirm worker sending, provider message ID and delivery timeline.
4. Simulate provider outage; create/post an invoice successfully while email retries.
5. Confirm permanent failure reaches DeadLetter.
6. Confirm hard bounce creates scoped suppression.
7. Remove suppression with a required reason and verify audit.
8. Send an internal message and verify per-recipient unread/read status.
9. Verify an unrelated user and tenant cannot access message or attachment.
10. Verify HR/Payroll confidentiality remains enforced.
11. Verify resend creates a new linked record.
12. Verify all `/communication` pages use the normal layout and navigation.
13. Run backend, frontend and migration validation used by this repository.

Record commands and exact results; do not claim unrun tests passed.

