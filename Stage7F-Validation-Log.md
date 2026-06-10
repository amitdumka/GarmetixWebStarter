# Stage 7F Validation Log

## Static validation

Command:

```bash
python3 scripts/validation/stage7f-static-checks.py
```

Result:

```text
PASS: Stage 7F version is 3.5.0 in frontend
PASS: Stage 7F build code in frontend
PASS: Stage 7F version is 3.5.0 in backend
PASS: Stage 7F build code in backend
PASS: Account group removed from main sidebar modules
PASS: Help group removed from main sidebar modules
PASS: Utility sidebar navigation contains only Admin
PASS: Workspace standalone footer button removed
PASS: Status footer now includes workspace access
PASS: Account footer dropdown still has profile link
PASS: Footer dropdown still has help links
PASS: Legacy shell revert option preserved

Stage 7F static checks passed.
```

## Package validation

ZIP integrity check passed.

## Not run in sandbox

- `dotnet build` — .NET SDK is not installed in this sandbox.
- Docker build — Docker is unavailable in this sandbox.
- `npm run build` — not run here; validate locally after extracting the package.
