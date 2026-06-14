# Stage 8B Package 2 Validation

Date: 2026-06-14

- [x] Backend authorization and API build with zero warnings and zero errors.
- [x] Ten xUnit role permission cases pass.
- [x] Nuxt production build passes; known external font certificate and sourcemap warnings remain tracked separately.
- [x] Owner receives universal policy access.
- [x] Store Manager receives permitted module entry but no Admin, edit, delete, or Payroll access.
- [x] HR and Payroll roles remain isolated to their assigned people module.
- [x] Docker API, web, and PostgreSQL services run successfully with healthy v4.1.1 runtime identity.
- [x] The live Access API returns ten role profiles and the Access workspace renders the same matrix.
- [x] The New User form exposes HR and Payroll roles, active status, and no independent Admin checkbox.
- [x] Access remains contained at 390 x 844 with document and viewport widths both equal to 390 pixels.
- [x] Existing unrelated backup metadata remains untouched.

## Browser Notes

- Desktop: user register, lifecycle actions, and role matrix loaded in the authenticated shell.
- Mobile: the permission table remains inside its horizontal scroll container and the page has no viewport overflow.
- Known residual: the shared authenticated shell still logs the existing hydration mismatch warning.
