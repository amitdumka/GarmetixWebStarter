export const garmetixModularVersion = {
  version: '6.0.62',
  stage: 'Stage 14F.9 Friendly API Error Messages',
  label: 'Version6 Stage 14F.9 Friendly API Error Messages',
  summary: 'Shared: fixed a codebase-wide bug in @garmetix/shared-api\'s request client - any non-JSON error response (an nginx/Cloudflare HTML error page, an empty body, plain text) was thrown verbatim as the error message, so infra-layer failures (like a transient 405 during a deploy window) showed raw "<html><head><title>405 Not Allowed</title>..." markup directly in the UI, including inside the Assistant chat panel. request() and loginToGarmetix() now try to parse the body as JSON and extract a real message ({error}/{message}/ProblemDetails {detail}/{title}, all fields the backend already uses) first, falling back to a clean status-code-based message ("This action is not available right now. Please try again in a moment." for 405, etc.) only when the body is not usable JSON - genuine backend validation messages are unaffected and still shown verbatim, only unusable bodies get replaced. Benefits every app using the shared client, not just the Assistant panel.'
} as const
