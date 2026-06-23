# Stage 8A Package 14 - Admin Workspaces

Version: 4.0.12
Build: `GARMETIX-8A-20260614-4012`
Date: 2026-06-14

## Completed

- Standardized Company Setup and Roles & Users with shared retryable register states.
- Added responsive overflow containment for company and user tables.
- Moved company/store and user access forms from narrow slideovers to wide modal workspaces.
- Replaced invalid empty Roles & Users scope options with an internal no-scope sentinel that is converted to null before saving.
- Preserved selected company, store-group, and store dates without UTC conversion.
- Added retained loading errors and direct retry actions to Company Onboarding and AF/SS Defaults.
- Added responsive loading skeletons to onboarding and seeding workspaces.
- Removed visible source-file, migration, MAUI mapping, and implementation language from business-facing Admin pages.
- Marked `/setup`, `/client-onboarding`, `/af-ss`, and `/access` reviewed in the Stage 8A audit queue.

## Preserved Behavior

- Existing company, store-group, store, user, onboarding, and AF/SS API contracts remain unchanged.
- Admin/Owner route restrictions remain active.
- Existing save, edit, delete, password reset, onboarding, and seeding operations remain available.

## Next

Continue the Stage 8A queue with remaining maintenance and system pages, then begin the Stage 8B role and permission acceptance matrix.
