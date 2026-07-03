# Stage 11D-135 — DotMatrix CSV Build Fix (v4.12.50)

## Purpose

Fix the API publish build blocker reported from Docker build:

`backend/Garmetix.Api/Production/PrintAcceptanceEndpoints.cs` around the `Csv` helper had malformed quote/newline literals.

## Change

Replaced the corrupted CSV escaping code with safe C# string/character literals:

- `value.Replace("\"", "\"\"")`
- `escaped.Contains('\"')`
- `escaped.Contains('\n')`
- `escaped.Contains('\r')`
- quoted output using `$"\"{escaped}\""`

## DotMatrix status

No behavior regression intended. This is a build-fix release on top of Stage 11D-134 DotMatrix Final Controls Closure.
