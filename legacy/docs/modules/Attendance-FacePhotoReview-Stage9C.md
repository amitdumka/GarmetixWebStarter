# Attendance Face Photo Review - Stage 9C

Stage 9C gives store managers and HR a review queue for kiosk photo proof attendance.

## Workflow

1. Employee marks attendance through the web kiosk.
2. Kiosk uploads a photo proof and punch metadata.
3. Manager opens `/attendance/photo-review`.
4. Manager approves, rejects, flags, or marks the record as needing regularization.
5. A regularization request can be created from the photo proof for correction/approval.

## Privacy rule

This stage stores proof photos and review metadata only. It does not perform biometric matching or liveness detection.
