# Stage 8I Next Three Production Acceptance v4.9.0

## 1. Data Cleanup

Open **Data → Data Consistency** and run checks.

Focus on:
- Duplicate Bank Accounts
- Date Cleanup
- Missing Journals

Correct duplicate/wrong records manually after taking a backup.

## 2. Backup/Restore Drill

Open **Maintenance → Backup Maintenance**.

Complete:
- Create fresh backup
- Verify all
- Upload latest to cloud
- Run restore preview/dry run
- Record operator note

Do not run a real restore on production unless you are intentionally recovering data.

## 3. GST Final Acceptance

Open **GST → GST Final Acceptance**.

Use it to check:
- Billing/Sales period data
- Purchase/ITC period data
- Accounting books
- GSTR-1
- GSTR-3B
- CA email and WhatsApp sharing
- Backup before filing

The checklist is an operator acceptance record and must still be reviewed by Accountant/CA before filing.
