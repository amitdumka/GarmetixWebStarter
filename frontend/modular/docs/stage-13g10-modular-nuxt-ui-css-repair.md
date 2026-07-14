# Stage 13G.10 Modular Nuxt UI CSS Repair

Version: 5.13.50

## Scope

This stage fixes the SRP public site rendering like a plain text page.

## Root Cause

The modular Nuxt apps had `@nuxt/ui` registered as a Nuxt module, but they did not include the required app CSS entry for Nuxt UI and Tailwind.

The generated production CSS file was therefore empty:

```text
/_nuxt/entry...css -> 0 bytes
```

JavaScript loaded and rendered the app content, but without Tailwind/Nuxt UI CSS every page looked like unstyled text and links.

## Fix

Each modular app now includes:

```ts
css: ['~/assets/css/main.css']
```

Each app also has:

```css
@import "tailwindcss";
@import "@nuxt/ui";
```

Updated apps:

- Main Back Office
- POS
- HR
- AI Sense
- Books
- Admin/SaaS

## Verification

The Main Back Office production build now emits a non-empty app CSS file:

```text
entry...css ~181 KB
```

## Deployment Note

After deployment, Cloudflare may keep the old zero-byte CSS in cache for a short time. A hard refresh or Cloudflare cache purge for `https://srp.aadwikafashion.in/_nuxt/*` clears the old asset immediately.
