# Stage 13G.23 - POS Counter Surface Polish

Version: 5.13.63

## Scope

- Applied the Option A hybrid Garmetix dashboard surface to POS counter pages.
- Covered POS dashboard, login, sale, day open, day close, held bills, print queue, sales returns and sales exchange.
- Aligned the Back Office login page with the same shared login card treatment.

## Behavior

- This stage is UI-only.
- No POS sale, return, exchange, hold bill, print, day open or day close request logic was changed.
- Existing local draft, held bill and print queue storage behavior remains untouched.

## Validation

- Run `npm run check` from `modular`.
- Build affected apps with `npm run build:pos` and `npm run build:main`.
- Deploy SRP with `npm run deploy:srp`.
- Verify live with `NODE_OPTIONS=--use-system-ca npm run deploy:srp:acceptance -- --live --strict`.

## Remaining Work

- Admin/SaaS still has the largest number of older raw surfaces and should be the next UI polish target.
- After Admin polish, add authenticated browser visual checks for POS sale, returns and day close flows.
