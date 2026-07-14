# Stage 6C — Public Info Pages + Version Identity

## Version

- Product: Garmetix
- Version: 2.2.0
- Stage: Stage 6C
- Release: Public Info Pages + Version Identity
- Build Date: 2026-06-10
- Build Code: GARMETIX-6C-20260610-220

## Implemented

### Backend

Added `backend/Garmetix.Api/AppInfo` module:

- `AppInfoDtos.cs`
- `AppInfoEndpoints.cs`

New endpoints:

- `GET /api/app-info`
- `GET /api/app-info/version`
- `GET /api/app-info/faq`
- `GET /api/app-info/contacts`

These endpoints are anonymous so About Us, Contact Us and FAQ can display identity/help information without needing special admin permissions.

### Frontend

Added central frontend version constants:

- `frontend/garmetix-web/utils/appVersion.ts`

Added pages:

- `/about-us`
- `/contact-us`
- `/faq`

Added sidebar menu group:

- Help → About Us
- Help → Contact Us
- Help → FAQ

Updated sidebar footer to show current stage/version:

- `Stage 6C · v2.2.0`

Updated `frontend/garmetix-web/package.json` version to `2.2.0`.

Updated API project metadata:

- `<Version>2.2.0</Version>`
- `<AssemblyVersion>2.2.0.0</AssemblyVersion>`
- `<FileVersion>2.2.0.0</FileVersion>`
- `<InformationalVersion>2.2.0-stage6c</InformationalVersion>`

## Version update rule

Every future code package should update both:

1. `backend/Garmetix.Api/AppInfo/AppInfoEndpoints.cs`
2. `frontend/garmetix-web/utils/appVersion.ts`

The validation script checks that backend and frontend versions/build codes match.
