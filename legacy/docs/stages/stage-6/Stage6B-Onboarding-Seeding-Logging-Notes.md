# Stage 6B — Onboarding/Seeder Result Fix + Message Logs

## Purpose

This stage fixes the false failure message shown after Client Onboarding and AF/SS seeding, and adds a separate Admin page to review saved operation messages with filters.

## Bug fixed

The Client Onboarding and AF/SS pages were showing a failed toast even after the main save/seed operation succeeded when the post-save refresh/options request failed. The save/seed call and the refresh call are now separated:

- successful submit/seed now immediately shows the success message returned by the backend;
- refresh errors are logged separately as `Post-save refresh failed` or `Post-seed refresh failed`;
- the successful result panel remains visible.

## Backend logging module added

New module:

```text
backend/Garmetix.Api/Messages/ApplicationMessageLogDtos.cs
backend/Garmetix.Api/Messages/ApplicationMessageLogService.cs
backend/Garmetix.Api/Messages/ApplicationMessageLogEndpoints.cs
```

New endpoints:

```text
GET  /api/message-logs/options
GET  /api/message-logs
POST /api/message-logs
```

The service creates an idempotent table if missing:

```text
ApplicationMessageLogs
```

The table stores:

- CreatedAtUtc
- Level
- Source
- EventName
- Message
- DetailsJson
- CompanyId / StoreGroupId / StoreId
- UserId / UserName
- Resource
- OperationId
- Success

## Operations now logged

Client Onboarding now logs:

- success after company/store/users/basic structure are created;
- validation failures;
- unexpected errors with technical details.

AF/SS Seeder now logs:

- success after default seed completes;
- missing company/profile validation failures;
- unexpected errors with technical details.

## Frontend page added

New Admin page:

```text
frontend/garmetix-web/pages/message-logs/index.vue
```

Admin menu link:

```text
Admin → Message Logs
```

Filter options:

- level
- source
- success/failure
- search text
- from date/time
- to date/time
- result limit

The page shows both saved backend logs and this-browser UI logs.

## Files changed

```text
backend/Garmetix.Api/Program.cs
backend/Garmetix.Api/Onboarding/ClientOnboardingEndpoints.cs
backend/Garmetix.Api/Seeds/AfssSeederEndpoints.cs
frontend/garmetix-web/pages/client-onboarding/index.vue
frontend/garmetix-web/pages/af-ss/index.vue
frontend/garmetix-web/pages/message-logs/index.vue
frontend/garmetix-web/components/AppShell.vue
```

## Runtime test steps

1. Build and run Docker.
2. Open `/client-onboarding` and submit a new company.
3. Confirm success toast and result panel are shown.
4. Open `/message-logs` and filter Source = `ClientOnboarding`.
5. Open `/af-ss`, seed a company.
6. Open `/message-logs` and filter Source = `AFSSSeeder`.

