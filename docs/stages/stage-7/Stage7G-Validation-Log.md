# Stage 7G Validation Log

Version: 3.6.0  
Build Code: GARMETIX-7G-20260610-360

## Static checks completed

- Confirmed `useAccessControl.ts` exists.
- Confirmed global route middleware now checks central access map.
- Confirmed `/access-denied` page exists.
- Confirmed AppShell uses `access.canAccessPath(...)` for visible menu filtering.
- Confirmed dropdown groups are sanitized by access policy.
- Confirmed Dashboard Map includes permission-aware access matrix.
- Confirmed version identity updated to 3.6.0 / Stage 7G in frontend and backend.
- Confirmed legacy shell revert option remains unchanged.
- Confirmed ZIP integrity passed.

## Not run in sandbox

- `dotnet build`, because .NET SDK is not installed in this sandbox.
- Docker build, because Docker is unavailable here.
- Full Nuxt production build, because local dependency/network constraints can affect icon/font metadata fetching.
