# Stage 6A Bugfix Validation Log

## Package

`Garmetix-Stage6A-FirstCompanyOnboarding-v2.0.1-executionstrategyfix.zip`

## Checks performed in sandbox

- Verified reported error is caused by manual EF Core transaction under `NpgsqlRetryingExecutionStrategy`.
- Patched `ClientOnboardingService.OnboardAsync` to use `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` around the transaction.
- Patched `AfssDefaultSeederService.SeedAsync` with the same pattern to prevent the same error in Admin → AF/SS.
- C# brace-balance static check passed for both modified files.
- ZIP integrity test passed.

## Checks not performed in sandbox

- `dotnet build` was not run because the sandbox does not have the .NET SDK.
- Docker build/runtime was not run because Docker is not available in the sandbox.

## Recommended local validation

```bash
cd backend
dotnet build

cd ..
docker compose up --build
```

Then retry:

```text
/client-onboarding
```

And optional:

```text
/af-ss
```
