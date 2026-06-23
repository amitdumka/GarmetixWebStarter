# Stage 9C - Face Photo Review v4.10.3

Version: `4.10.3`  
Stage: `Stage 9C Face Photo Review`  
Release: `Face Photo Review and Attendance Approval Foundation`  
Build code: `GARMETIX-9C-20260619-4103`

## Purpose

Stage 9C extends Stage 9B kiosk photo proof by adding manager review and approval workflow for captured attendance photos.

## Included

- Face Photo Review page at `/attendance/photo-review`.
- Photo proof review summary with pending, approved, rejected, flagged, needs regularization, and expiring-soon counts.
- Approve / reject / flag / needs regularization actions.
- Optional automatic regularization request creation from a photo proof review.
- Review audit fields: status, reviewer, reviewed time, reason, remarks, and linked regularization request.
- Schema repair and migration for existing PostgreSQL volumes.

## Not included

- No AI face recognition.
- No liveness detection.
- No fingerprint matching.
- No payroll auto-deduction.
- No raw fingerprint image storage.

## Test

```bash
python3 scripts/validation/current-release-checks.py
export GARMETIX_SMOKE_USER='admin'
export GARMETIX_SMOKE_PASSWORD='your-admin-password'
./scripts/linux/attendance-face-photo-review-drill.sh .env.production
```
