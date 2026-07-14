# Stage 13G.14 SRP Modular App Asset Repair

## Problem

The SRP public deployment served POS, HR, AI Sense, Books and Admin HTML at their path prefixes, but those generated HTML files referenced Nuxt assets from `/_nuxt/` instead of each app base path such as `/pos/_nuxt/`.

Nginx then served Back Office fallback HTML for those root asset requests. The browser received HTML where JavaScript or CSS was expected, which made the non-main modular apps appear blank.

## Repair

- Replaced the Nuxt build base-path override with `GARMETIX_NUXT_BASE_URL`.
- Updated all modular app Nuxt configs to read `GARMETIX_NUXT_BASE_URL` before app-specific base-path variables.
- Updated the SRP deploy script so each app build receives the correct base path.
- Cleared the shared Nuxt workspace cache before each app build so the Back Office `/` base path cannot leak into POS, HR, AI Sense, Books or Admin builds.
- Hardened the static release patch step to rewrite Nuxt script/style links and `baseURL` in generated HTML to the final deployed app base path.
- Expanded SRP public acceptance to verify that each app HTML references JavaScript and CSS under its own app base path and that those assets are served with JavaScript/CSS content types.

## Current Scope

This stage repairs the current SRP path-based deployment:

- `/` Back Office
- `/pos/` POS
- `/hr/` HR
- `/ai-sense/` AI Sense
- `/books/` Books
- `/admin/` Admin/SaaS

True hostname-based deployment remains the target architecture:

- `pos.srp.aadwikafashion.in`
- `hr.srp.aadwikafashion.in`
- `ai-sense.srp.aadwikafashion.in`
- `books.srp.aadwikafashion.in`
- `admin.srp.aadwikafashion.in`
- later production equivalents under `garmetix.aadwikafashion.in`

The next deployment stage should add host-based static roots and Cloudflare ingress entries after DNS hostnames are ready.
