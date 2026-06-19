export const APP_VERSION = '4.10.4'
export const APP_STAGE = 'Stage 9D Attendance Payroll Integration Foundation'
export const APP_RELEASE_NAME = 'Attendance Payroll Review Foundation'
export const APP_BUILD_DATE = '2026-06-18'
export const APP_BUILD_CODE = 'GARMETIX-9D-20260619-4104'

export const APP_HIGHLIGHTS = [
  'Stage 9D adds attendance payroll review rows with payable days, deduction days, overtime minutes, lock visibility, and review status without posting payroll.',
  'Attendance payroll review can be rebuilt from monthly attendance summaries and marked Reviewed, ApprovedForPayroll, or OnHold.',
  'Monthly attendance generation now fully qualifies the legacy HRM Attendance entity to avoid namespace/type collision during Docker API publish.',
  'Stage 9C adds manager face photo proof review, approve/reject/flag workflow, regularization linkage, retention visibility, and audit-ready photo status.',
  'Kiosk API base includes device registration, heartbeat, employee lookup, punch and sync-pending endpoints for future Android/MAUI kiosk apps.',
  'Face attendance is limited to photo-proof path placeholders; real face recognition and liveness checks remain future packages.',
  'Fingerprint support is limited to enrollment/device bridge placeholders; no raw fingerprint image is stored.',
  'Payroll attendance summary is available for review only; automatic salary deduction/posting remains a later package.',
  'Employee save now avoids EF Core DefaultIfEmpty translation failures during PUT /api/employees.',
  'Deprecated lucide-vue-next dependency removed; Nuxt UI lucide icons remain through @iconify-json/lucide.',
  'HR schema repair now auto-adds missing Package 22/23 employee columns and the EmployeePayrollAdjustments table on existing PostgreSQL volumes.',
  'HR Employee Master now supports auto employee code, photo preview, document/bank fields, lifecycle status and printable ID-card readiness.',
  'HR Benefits adds salary advance, advance recovery, leave, bonus, leave encashment, PF and gratuity adjustment workflow for payroll.',
  'Payroll generation and payment preview now consider HR Benefits adjustments such as advances, bonus, PF and gratuity rows.',
  'License Activation now has a dedicated admin page for signed client license generation, activation and status review.',
  'The API includes HMAC-signed offline license keys, masked license status and optional operational API enforcement.',
  'Docker/env templates now include LICENSE_* settings plus a persistent /app/license activation volume.',
  'A host-level license acceptance drill verifies status, optional activation and protected API blocking behavior.',
  'Test Automation now includes LICENSE_SAAS_ACTIVATION so licensing remains part of production readiness.',
  'SMTP, print/PDF, backup/restore, permission, secret hygiene and Docker acceptance checks remain in the release manifest.'
]
