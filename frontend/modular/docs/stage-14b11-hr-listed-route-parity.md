# Stage 14B.11 HR Listed Route Parity

Version: `6.0.20`

Status: `complete`

## Routes Compared

| Legacy URL | Modular local route | Status | Backend/API source |
| --- | --- | --- | --- |
| `https://garmetix.aadwikafashion.in/hr` | `/hr` | Implemented | `/api/employees`, `/api/hr/employee-master/summary`, `/api/hr/attendance`, `/api/hr/monthly-attendance/generate`, CRUD `/api/attendance` |
| `https://garmetix.aadwikafashion.in/hr-benefits` | `/hr-benefits` | Implemented | `/api/hr-payroll/adjustments`, `/api/hr-payroll/adjustments/summary`, `/api/employees` |
| `https://garmetix.aadwikafashion.in/attendance` | `/attendance` | Implemented | `/api/attendance/today`, `/api/employees`; route hub links to attendance submodules |
| `https://garmetix.aadwikafashion.in/attendance/shifts` | `/attendance/shifts` | Implemented | `/api/attendance/shifts` |
| `https://garmetix.aadwikafashion.in/attendance/shift-rules` | `/attendance/shift-rules` | Implemented in 6.0.19 | `/api/attendance/shift-rules`, `/api/attendance/shifts`, `/api/employees` |
| `https://garmetix.aadwikafashion.in/attendance/policies` | `/attendance/policies` | Implemented | `/api/attendance/policies` |

## Legacy Features Ported

- `/hr`: employee CRUD/edit form, readiness metrics, old daily attendance register, daily attendance create/edit form and monthly attendance generation action.
- `/hr-benefits`: salary advance, recovery, leave, bonus, leave encashment, PF, gratuity and other payroll adjustment register with summary metrics.
- `/attendance`: attendance command hub with today's counts and links to kiosk, devices, shifts, policies, payroll review, salary draft/payment and review pages.
- `/attendance/shifts`: shift list, presets, create/edit/delete, split-shift settings, break/session fields and protected default handling.
- `/attendance/shift-rules`: employee/category/department/designation/gender/store-default assignment rules.
- `/attendance/policies`: grace, late, half-day, minimum day, overtime, duplicate window and auto-checkout policy setup.

## Remaining HR Parity Notes

- `/hr` now covers the legacy core flows, but ID-card preview/print and photo file upload polish still need a dedicated pass.
- `/payroll` still needs full legacy salary structure CRUD, salary payment CRUD and payslip print/share controls.
- Kiosk/device/photo/mobile pages exist in modular HR and still need a second visual/contract pass against the latest v4.12.69 legacy screens.

## Validation

Run:

```bash
npm run modular:hr:listed-route-parity
npm run modular:check
npm --prefix frontend/modular run build:hr
```
